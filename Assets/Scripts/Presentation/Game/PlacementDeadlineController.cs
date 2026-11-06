using Commander.Commands;
using Domain.Services;
using UnityEngine;

namespace Presentation.Game
{
    /// <summary>
    /// One per human board root. Owns the per-board placement deadline: it accrues idle, ready,
    /// human, not-game-over time and, at expiry, enqueues exactly one random-side auto-placement
    /// through the board's normal command path. Accrual is suspended while the queue is busy
    /// (which covers FreezeBall) and while the app is backgrounded, and it resets only when a
    /// ball actually leaves the front of the queue on an accepted placement — a refused tap, a
    /// <c>SwitchBalls</c> or a freeze never resets it. The AI board never counts. The visual
    /// pressure is produced elsewhere: <see cref="BalanceDangerShakeView"/> pulls
    /// <see cref="CurrentRisk"/>.
    /// </summary>
    public sealed class PlacementDeadlineController : MonoBehaviour
    {
        [SerializeField] private Balance balance;
        [SerializeField] private BallZone ballZone;
        [SerializeField, Min(1f)] private float limitSeconds = 10f;

        private readonly System.Random _random = new();

        // Reset state: the front ball is snapshotted when the queue first becomes busy, and the
        // settle edge resets only when that ball actually left the queue (gone from both index 0
        // and index 1). A placement consumes it, whereas SwitchBalls merely relocates it to index
        // 1, so a refused tap, a freeze or a switch never resets the clock.
        private bool _expired;
        private bool _wasBusy;
        private bool _hasFocus = true;
        private bool _isPaused;
        private float _elapsed;
        private Ball _lastFrontBall;
        private Ball _frontBallBeforeBusy;

#if UNITY_EDITOR
        private BalanceRisk _lastLoggedRisk = BalanceRisk.Safe;
#endif

        /// <summary>Deadline risk for the current remaining time; <see cref="BalanceRisk.Safe"/> while not counting.</summary>
        public BalanceRisk CurrentRisk { get; private set; } = BalanceRisk.Safe;

        /// <summary>Seconds left on the current deadline, floored at zero.</summary>
        public float RemainingSeconds => Mathf.Max(0f, limitSeconds - _elapsed);

        /// <summary>True only while the clock is actually accruing this frame.</summary>
        public bool IsCounting { get; private set; }

        /// <summary>True while the app is unfocused or paused; the clock must not accrue then.</summary>
        private bool Suspended => !_hasFocus || _isPaused;

        // Lifecycle suspension: the deadline pauses while the app is unfocused or backgrounded.
        // The two flags are OR-ed: accrual stays frozen while EITHER reports inactive (unfocused
        // OR paused), so a normal regain of focus resumes the clock even when the focus/pause
        // events do not pair up cleanly in the editor.
        private void OnApplicationFocus(bool hasFocus)
        {
            _hasFocus = hasFocus;
        }

        private void OnApplicationPause(bool paused)
        {
            _isPaused = paused;
        }

        private void Update()
        {
            var gameManager = FindFirstObjectByType<GameManager>();
            var player = balance != null ? balance.GetPlayer() : null;

            if (balance == null || ballZone == null || gameManager == null || player == null)
            {
                IsCounting = false;
                CurrentRisk = BalanceRisk.Safe;
                _wasBusy = false;
                _frontBallBeforeBusy = null;
                return;
            }

            var busy = player.IsBusy();
            var frontBall = ballZone.GetBall(0);

            // Snapshot the front ball when the queue first becomes busy: this is the ball a real
            // placement would consume. A tap that arrives while already busy is refused by the
            // player and adds no command, so it cannot start a new busy window — and a SwitchBalls
            // inside the current window cannot re-enter this branch either.
            if (busy && !_wasBusy)
            {
                _frontBallBeforeBusy = _lastFrontBall;
            }

            // A settle resets the clock only when the pre-busy front ball actually left the queue:
            // it must be gone from BOTH index 0 and index 1. A placement consumes it (gone), but
            // SwitchBalls only swaps it into index 1, so that path never resets.
            if (_wasBusy && !busy)
            {
                if (_frontBallBeforeBusy != null
                    && !ReferenceEquals(ballZone.GetBall(0), _frontBallBeforeBusy)
                    && !ReferenceEquals(ballZone.GetBall(1), _frontBallBeforeBusy))
                {
                    Reset();
                }
            }

            var counting = IsHumanBoard(player, gameManager)
                           && CanCount(player, gameManager, busy, frontBall)
                           && !Suspended;
            if (counting)
            {
                _elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.1f);
                IsCounting = true;
            }
            else
            {
                IsCounting = false;
            }

            if (!_expired && counting && _elapsed >= limitSeconds)
            {
                // Re-check idle and the front ball immediately before enqueue — no await happens
                // in between, so a same-frame tap cannot double-place. If the queue is refilling
                // (ball 0 null), place nothing and retry on a later frame. Snapshot the current
                // front ball (still pre-pop, since the pop is synchronous inside AddCommand) so the
                // settle edge recognises this placement.
                if (!player.IsBusy() && ballZone.GetBall(0) != null)
                {
                    _frontBallBeforeBusy = frontBall;
                    player.AddCommand(new TryBallInBalanceCommand(
                        ballZone, PlacementDeadlinePolicy.ExpirySide(_random), balance));
                    _expired = true;
                }
            }

            CurrentRisk = counting
                ? PlacementDeadlinePolicy.Risk(RemainingSeconds, limitSeconds)
                : BalanceRisk.Safe;

#if UNITY_EDITOR
            if (CurrentRisk != _lastLoggedRisk)
            {
                _lastLoggedRisk = CurrentRisk;
                Debug.Log(
                    $"[PlacementDeadline] '{name}' risk {CurrentRisk}, remaining {RemainingSeconds:0.0}s", this);
            }
#endif

            _lastFrontBall = frontBall;
            _wasBusy = busy;
        }

        private void Reset()
        {
            _elapsed = 0f;
            _expired = false;
        }

        private bool CanCount(IPlayer player, GameManager gameManager, bool busy, Ball frontBall)
        {
            if (_expired) return false;
            if (busy) return false;
            if (!player.IsReadyToPlay()) return false;
            if (player.IsGameOver()) return false;
            if (gameManager.IsGameOver()) return false;
            if (!ballZone.IsInputEnabled()) return false;
            return frontBall != null;
        }

        private bool IsHumanBoard(IPlayer player, GameManager gameManager)
        {
            // Identity, not type: the first human or any board in local PvP. The AI board and the
            // tutorial's inactive P2 root never count.
            return ReferenceEquals(player, gameManager.GetFirstPlayer()) || gameManager.IsLocalPvP();
        }
    }
}
