using System.Collections.Generic;
using Domain.Services;
using UnityEngine;

namespace Presentation.Game
{
    /// <summary>
    /// One per human board root. Pulls <see cref="PlacementDeadlineController.CurrentRisk"/> and
    /// vibrates the visual children of the pending-ball queue, so the deadline pressure reads on the
    /// row itself instead of the board. The board vibration is owned elsewhere:
    /// <see cref="BalanceDangerShakeView"/> remains the sole writer of the board root and reacts to
    /// board risk only. This view is the sole writer of the queue balls' renderer children; it never
    /// touches a ball root transform or its collider. The AI board and the game-over/tutorial-inactive
    /// cases already read <see cref="BalanceRisk.Safe"/> from the controller, so nothing is written
    /// for them. Visual only: no audio, no haptics, and nothing that changes placement, scoring,
    /// outcome or save data.
    /// </summary>
    public sealed class PendingBallsDeadlineShakeView : MonoBehaviour
    {
        [SerializeField] private BallZone ballZone;
        [SerializeField] private PlacementDeadlineController deadline;

        [Header("Vibration tuning (translation on queue-ball renderer children)")]
        [SerializeField] private float mildFrequencyHz = 14f;
        [SerializeField] private float strongFrequencyHz = 20f;
        [SerializeField] private float mildAmplitude = 0.012f;
        [SerializeField] private float strongAmplitude = 0.022f;
        [Tooltip("Perlin-noise share mixed into the sine so it reads as a buzz and not a click.")]
        [SerializeField] private float noiseAmount = 0.25f;

        // Every visual-child transform we are currently driving, mapped to its authored base
        // local position so the exact pose is restored when the row stops vibrating, a ball leaves
        // the queue, or this view is disabled/destroyed.
        private readonly Dictionary<Transform, Vector3> _targets = new();

        private BalanceRisk _effectiveRisk = BalanceRisk.Safe;
        private float _phaseStart;
        private float _phase;

        private void OnDisable()
        {
            RestoreAndClear();
        }

        private void OnDestroy()
        {
            RestoreAndClear();
        }

        private void LateUpdate()
        {
            // Restore every target we currently drive back to its base pose first. A ball that
            // left the queue since the last frame is not re-added below, so this is also the exact
            // "restore on departure" path. A destroyed transform is skipped (Unity fake-null).
            foreach (var entry in _targets)
            {
                if (entry.Key != null) entry.Key.localPosition = entry.Value;
            }
            _targets.Clear();

            var risk = deadline != null ? deadline.CurrentRisk : BalanceRisk.Safe;
            if (risk == BalanceRisk.Safe || ballZone == null)
            {
                // A cleared risk restarts the phase on the next pressure window.
                _effectiveRisk = BalanceRisk.Safe;
                return;
            }

            if (risk != _effectiveRisk)
            {
                // A level change restarts the phase without re-capturing, so a mid-vibration pose
                // is never frozen as a base.
                _effectiveRisk = risk;
                _phaseStart = Time.unscaledTime;
                _phase = 0f;
            }

            // Discover the visual children of every non-null pending-ball slot. Only
            // renderer-bearing children are visual (Art, ArtBall, ArtShell, ...); the ball root,
            // its collider and the VFX points have no renderer and are never written. Sombra sits
            // under Art and moves with it, so it is covered by the Art transform.
            var slotCount = ballZone.SlotCount;
            for (var i = 0; i < slotCount; i++)
            {
                var ball = ballZone.GetBall(i);
                if (ball == null) continue;
                var root = ball.transform;
                if (root == null) continue;

                for (var c = 0; c < root.childCount; c++)
                {
                    var child = root.GetChild(c);
                    if (child.GetComponent<Renderer>() == null) continue;
                    _targets[child] = child.localPosition;
                }
            }

            if (_targets.Count == 0) return;

            var (amplitude, frequencyHz) = Tuning(_effectiveRisk);
            var offset = amplitude * Vibration(Time.unscaledTime - _phaseStart, Time.unscaledDeltaTime, frequencyHz);

            // Coherent row motion: the same horizontal offset for every ball, translation only
            // (round sprites).
            foreach (var entry in _targets)
            {
                entry.Key.localPosition = entry.Value + new Vector3(offset, 0f, 0f);
            }
        }

        private float Vibration(float elapsed, float deltaTime, float frequencyHz)
        {
            // Same phase accumulation as the board, minus the breathing envelope: the row buzzes
            // at a steady amplitude so the queue keeps reading as one moving row. Never rebuild
            // the phase from a per-frame frequency times the absolute elapsed time: at dt = 0.5/f
            // the samples land on multiples of PI and the sine is identically 0.
            var frequency = EffectiveFrequency(frequencyHz, deltaTime);
            _phase = Mathf.Repeat(_phase + 2f * Mathf.PI * frequency * deltaTime, 2f * Mathf.PI);

            var wave = Mathf.Sin(_phase);
            var noise = Mathf.PerlinNoise(elapsed * 30f, 0.37f) * 2f - 1f;
            return (wave + noise * noiseAmount) / (1f + noiseAmount);
        }

        private static float EffectiveFrequency(float frequencyHz, float deltaTime)
        {
            // Hard cap at 20 Hz (about three frames per cycle at 60 fps) and never the exact
            // Nyquist limit of the frame: 0.4/dt keeps at least 2.5 samples per cycle, so the
            // sampled sine never collapses to zero the way 0.5/dt does.
            var usable = 0.4f / Mathf.Max(deltaTime, 1e-4f);
            return Mathf.Min(frequencyHz, Mathf.Min(20f, usable));
        }

        private void RestoreAndClear()
        {
            foreach (var entry in _targets)
            {
                if (entry.Key != null) entry.Key.localPosition = entry.Value;
            }

            _targets.Clear();
            _effectiveRisk = BalanceRisk.Safe;
        }

        private (float amplitude, float frequencyHz) Tuning(BalanceRisk risk)
        {
            return risk == BalanceRisk.Strong
                ? (strongAmplitude, strongFrequencyHz)
                : (mildAmplitude, mildFrequencyHz);
        }
    }
}
