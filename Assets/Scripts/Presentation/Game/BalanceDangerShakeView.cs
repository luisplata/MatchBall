using System;
using Domain.Services;
using UnityEngine;

namespace Presentation.Game
{
    /// <summary>
    /// One per human board root. Reads <see cref="Balance.CurrentRisk"/> and
    /// <see cref="PlacementDeadlineController.CurrentRisk"/>, pulses a brief frame shake at
    /// <c>max(boardRisk, deadlineRisk)</c> while the board is one or two moves from a loss or its
    /// deadline is running out. This view is the sole writer of the board-root transform. The AI
    /// board never shakes; the pulse stops and the authored pose is restored the moment the risk
    /// clears, the match ends, or the view is disabled/destroyed. Visual only: no audio, no
    /// haptics, and nothing that changes placement, scoring, outcome or save data.
    /// </summary>
    public sealed class BalanceDangerShakeView : MonoBehaviour
    {
        [SerializeField] private Balance balance;
        [Tooltip("Optional. Defaults to this transform (the board root). Leave empty.")]
        [SerializeField] private Transform shakeTarget;
        [SerializeField] private PlacementDeadlineController deadline;

        private const float MildAmplitude = 0.03f;
        private const float MildTiltDegrees = 0.6f;
        private const float MildInterval = 2.0f;
        private const float StrongAmplitude = 0.06f;
        private const float StrongTiltDegrees = 1.2f;
        private const float StrongInterval = 1.1f;
        private const float PulseDuration = 0.18f;

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
            var effective = BalanceRisk.Safe;
            if (IsHumanBoard() && !IsGameOver())
            {
                // The deadline never adds a writer: it only feeds a second risk input, and this
                // view stays the sole transform writer. A null deadline preserves pre-change
                // behavior (board risk only).
                var deadlineRisk = deadline != null ? deadline.CurrentRisk : BalanceRisk.Safe;
                effective = (BalanceRisk)Math.Max((int)_boardRisk, (int)deadlineRisk);
            }

            if (effective == BalanceRisk.Safe)
            {
                StopPulse();
                return;
            }

            if (effective != _effectiveRisk)
            {
                // Capture the base pose only now, never in OnEnable, where the board's
                // orientation may not have been applied yet. A level change restarts the phase
                // without re-capturing (so a mid-pulse pose is never frozen as the base).
                CaptureBase();
                _effectiveRisk = effective;
                _phaseStart = Time.unscaledTime;
            }

            if (!_hasBase || _target == null) return;

            var (amplitude, tilt, interval) = Tuning(_effectiveRisk);
            var phase = (Time.unscaledTime - _phaseStart) % interval;
            if (phase >= PulseDuration)
            {
                // Intermittent, never continuous: idle between pulses at the exact base pose.
                _target.localPosition = _baseLocalPosition;
                _target.localRotation = _baseLocalRotation;
                return;
            }

            var t = phase / PulseDuration;
            var strength = Mathf.Sin(Mathf.PI * t);

            _target.localPosition = _baseLocalPosition + new Vector3(amplitude * strength, 0f, 0f);
            _target.localRotation = _baseLocalRotation * Quaternion.Euler(0f, 0f, tilt * strength);
        }

        private static (float amplitude, float tilt, float interval) Tuning(BalanceRisk risk)
        {
            return risk == BalanceRisk.Strong
                ? (StrongAmplitude, StrongTiltDegrees, StrongInterval)
                : (MildAmplitude, MildTiltDegrees, MildInterval);
        }
    }
}
