using Domain.Services;
using UnityEngine;

namespace Presentation.Game
{
    /// <summary>
    /// One per human board root. Reads <see cref="Balance.CurrentRisk"/> and vibrates the board
    /// continuously while the board is one or two moves from a loss. This view is the sole writer of
    /// the board-root transform; deadline pressure is shown by
    /// <see cref="PendingBallsDeadlineShakeView"/> on the queue row, not here. The AI board never
    /// vibrates; the vibration stops and the authored pose is restored the moment the risk clears,
    /// the match ends, or the view is disabled/destroyed. Visual only: no audio, no haptics, and
    /// nothing that changes placement, scoring, outcome or save data.
    /// </summary>
    public sealed class BalanceDangerShakeView : MonoBehaviour
    {
        [SerializeField] private Balance balance;
        [Tooltip("Optional. Defaults to this transform (the board root). Leave empty.")]
        [SerializeField] private Transform shakeTarget;

        [Header("Vibration tuning (translation and tilt on the board root)")]
        [SerializeField] private float mildFrequencyHz = 14f;
        [SerializeField] private float strongFrequencyHz = 20f;
        [SerializeField] private float mildAmplitude = 0.005f;
        [SerializeField] private float strongAmplitude = 0.011f;
        [SerializeField] private float mildTiltDegrees = 0.15f;
        [SerializeField] private float strongTiltDegrees = 0.30f;
        [Tooltip("Slow amplitude breathing (floor -> 1x). Board only; the queue row does not breathe.")]
        [SerializeField] private float breathingHz = 1.5f;
        [SerializeField] private float breathingFloor = 0.7f;
        [Tooltip("Perlin-noise share mixed into the sine so it reads as a buzz and not a click.")]
        [SerializeField] private float noiseAmount = 0.25f;

        private GameManager _gameManager;
        private Transform _target;
        private Vector3 _baseLocalPosition;
        private Quaternion _baseLocalRotation;
        private bool _hasBase;
        private BalanceRisk _boardRisk = BalanceRisk.Safe;
        private BalanceRisk _effectiveRisk = BalanceRisk.Safe;
        private float _phaseStart;

        private void OnEnable()
        {
            _gameManager = FindFirstObjectByType<GameManager>();
            if (balance == null)
            {
                Debug.LogWarning($"[BalanceDangerShakeView] no balance reference on '{name}'.", this);
                enabled = false;
                return;
            }

            balance.RiskChanged += OnRiskChanged;
            _boardRisk = balance.CurrentRisk;
        }

        private void OnDisable()
        {
            if (balance != null) balance.RiskChanged -= OnRiskChanged;
            RestoreBase();
            _hasBase = false;
            _effectiveRisk = BalanceRisk.Safe;
        }

        private void OnDestroy()
        {
            if (balance != null) balance.RiskChanged -= OnRiskChanged;
            RestoreBase();
        }

        private void OnRiskChanged(BalanceRisk risk)
        {
            _boardRisk = risk;
        }

        private bool IsHumanBoard()
        {
            if (_gameManager == null) return false;
            var player = balance.GetPlayer();
            if (player == null) return false;

            // Identity, not type: a human P2 is not a MachinePlayer, so test who owns this
            // board against the first player, and let every local-PvP board through.
            return ReferenceEquals(player, _gameManager.GetFirstPlayer()) || _gameManager.IsLocalPvP();
        }

        private bool IsGameOver()
        {
            if (_gameManager != null && _gameManager.IsGameOver()) return true;
            var player = balance.GetPlayer();
            return player != null && player.IsGameOver();
        }

        private Transform Target => shakeTarget != null ? shakeTarget : transform;

        private void CaptureBase()
        {
            if (_hasBase) return;
            _target = Target;
            _baseLocalPosition = _target.localPosition;
            _baseLocalRotation = _target.localRotation;
            _hasBase = true;
        }

        private void StopPulse()
        {
            RestoreBase();
            _hasBase = false;
            _effectiveRisk = BalanceRisk.Safe;
        }

        private void RestoreBase()
        {
            if (!_hasBase || _target == null) return;
            _target.localPosition = _baseLocalPosition;
            _target.localRotation = _baseLocalRotation;
        }

        private void LateUpdate()
        {
            var effective = IsHumanBoard() && !IsGameOver() ? _boardRisk : BalanceRisk.Safe;

            if (effective == BalanceRisk.Safe)
            {
                StopPulse();
                return;
            }

            if (effective != _effectiveRisk)
            {
                // Capture the base pose only now, never in OnEnable, where the board's
                // orientation may not have been applied yet. A level change restarts the phase
                // without re-capturing (so a mid-vibration pose is never frozen as the base).
                CaptureBase();
                _effectiveRisk = effective;
                _phaseStart = Time.unscaledTime;
            }

            if (!_hasBase || _target == null) return;

            var (amplitude, tilt, frequencyHz) = Tuning(_effectiveRisk);
            var strength = Vibration(Time.unscaledTime - _phaseStart, frequencyHz);

            // Always written from the captured base, never from the current pose: no drift.
            _target.localPosition = _baseLocalPosition + new Vector3(amplitude * strength, 0f, 0f);
            _target.localRotation = _baseLocalRotation * Quaternion.Euler(0f, 0f, tilt * strength);
        }

        private float Vibration(float elapsed, float frequencyHz)
        {
            // Continuous sine at the tuned frequency, mixed with Perlin noise. Noise (not Random)
            // breaks the regularity without the shimmer white noise adds at 60 fps, and stays a
            // pure function of time, so every board reads the same buzz.
            var frequency = EffectiveFrequency(frequencyHz);
            var wave = Mathf.Sin(2f * Mathf.PI * frequency * elapsed);
            var noise = Mathf.PerlinNoise(elapsed * 30f, 0.37f) * 2f - 1f;
            var strength = (wave + noise * noiseAmount) / (1f + noiseAmount);

            // Slow breathing so the buzz never sits at full amplitude for a whole risk window.
            var breathing = Mathf.Lerp(
                breathingFloor,
                1f,
                0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * breathingHz * elapsed));

            return breathing * strength;
        }

        private static float EffectiveFrequency(float frequencyHz)
        {
            // Hard cap at 20 Hz (about three frames per cycle at 60 fps), and never above the
            // Nyquist limit of the current frame: past it the sine would alias into a frozen
            // offset instead of vibrating.
            var nyquist = 0.5f / Mathf.Max(Time.unscaledDeltaTime, 1e-4f);
            return Mathf.Min(frequencyHz, Mathf.Min(20f, nyquist));
        }

        private (float amplitude, float tilt, float frequencyHz) Tuning(BalanceRisk risk)
        {
            return risk == BalanceRisk.Strong
                ? (strongAmplitude, strongTiltDegrees, strongFrequencyHz)
                : (mildAmplitude, mildTiltDegrees, mildFrequencyHz);
        }
    }
}
