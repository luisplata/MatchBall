using Bootstrap;
using Commander;
using Domain.Services;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Presentation.Tutorial
{
    /// <summary>
    /// Glue between the real board and the pure <see cref="TutorialFlow"/>. It only runs when
    /// <see cref="GameManager"/> consumed the transient tutorial flag, so it can never influence a
    /// normal match. It never awards XP or gold: finishing routes straight to BallPass, and an
    /// overflow becomes a retry (scene reload) instead of a real game over.
    /// </summary>
    public sealed class TutorialController : MonoBehaviour
    {
        [SerializeField] private TutorialOverlay overlay;

        /// <summary>
        /// Wall-clock delay from the scene start until the overlay shows itself. It is the single
        /// source of truth for "when does the lesson appear": nothing else gates the reveal.
        /// </summary>
        [Tooltip("Seconds after the tutorial scene starts before the tutorial graphics appear. "
                 + "0 = first frame. This is the only thing that decides when they show.")]
        [Min(0f)] [SerializeField] private float graphicsDelaySeconds = 1.2f;

        private TutorialFlow _flow;
        private GameManager _gameManager;
        private IPlayer _player;
        private Balance _balance;

        private bool _active;
        private bool _exiting;
        private bool _retryOffered;
        private bool _pendingTap;
        private bool _busyObserved;
        private float _openingWaitSeconds;
        private PathType _pendingSide;
        private int _settledBallCount;
        private int _ballsBeforeTap;
        private TutorialStep _retryStep;

        private static IStorageData Storage => ServiceLocator.Instance.GetService<IStorageData>();

        /// <summary>
        /// The prefab ships visible and this controller only binds in <see cref="Start"/>, so the
        /// lesson would show through the splash. Hiding in Awake leaves no rendered frame showing it:
        /// the overlay only comes back once <see cref="graphicsDelaySeconds"/> has elapsed.
        /// </summary>
        private void Awake()
        {
            if (overlay != null) overlay.Hide();
        }

        private void Start()
        {
            _gameManager = FindFirstObjectByType<GameManager>();
            if (_gameManager == null || !_gameManager.IsTutorialMode())
            {
                if (_gameManager != null) _gameManager.RestoreTutorialOpponentHud();
                overlay.Hide();
                enabled = false;
                return;
            }

            _player = _gameManager.GetFirstPlayer();
            _balance = _player.GetBalance() as Balance;
            if (_balance == null)
            {
                Debug.LogError("[Tutorial] the first player has no Balance; tutorial disabled.");
                _gameManager.RestoreTutorialOpponentHud();
                overlay.Hide();
                enabled = false;
                return;
            }

            // The scene carries no override for this prefab: the bubble and the hand resolve their
            // anchors from the live board the controller already owns.
            overlay.Bind(
                Camera.main,
                _gameManager.GetOpponentBoardRoot(),
                _balance.GetLeftPath() != null ? _balance.GetLeftPath().GetComponent<BoxCollider2D>() : null,
                _balance.GetRightPath() != null ? _balance.GetRightPath().GetComponent<BoxCollider2D>() : null);

            _flow = new TutorialFlow(TutorialResume.Step);
            _balance.OnBalanceClicked += OnBalanceClicked;
            _balance.OnGetPoints += OnPointsAwarded;
            _balance.RiskChanged += OnRiskChanged;
            overlay.Initialize(OnSkip, OnContinue, OnRetry);

            // Not `_active` yet: Update waits for graphicsDelaySeconds in TickReveal before the
            // first step is ever drawn, so the splash and the board load stay free of tutorial UI.
            Debug.Log($"[Tutorial] started at step {_flow.Step} (resume {TutorialResume.Step})");
        }

        private void Update()
        {
            if (_exiting) return;

            // Held: the overlay is down and the only work is waiting for the graphics delay.
            if (!_active)
            {
                TickReveal();
                return;
            }

            if (_retryOffered) return;

            if (_player.IsGameOver())
            {
                Handle(_flow.OnOverflow());
                return;
            }

            var busy = ServiceLocator.Instance.TryGetService<ICommandQueue>(out var queue) && queue != null
                       && queue.IsBusy(_player);

            if (_pendingTap)
            {
                _pendingTap = false;
                // The queue only goes busy when Player.OnBalanceClicked accepted the tap; a refused
                // tap (still animating) must not advance the curriculum.
                if (!busy) return;

                _busyObserved = true;
                _ballsBeforeTap = _settledBallCount;
                Handle(_flow.OnTap(_pendingSide));
                return;
            }

            if (_busyObserved)
            {
                if (busy) return;

                _busyObserved = false;
                Handle(_flow.OnSettled());
                return;
            }

            _settledBallCount = CountBalls();
        }

        /// <summary>
        /// Holds the lesson back until <see cref="graphicsDelaySeconds"/> has elapsed since the scene
        /// started. That delay is the only input: the opening countdown neither shortens nor extends
        /// it. The clock is unscaled, so a paused <c>timeScale</c> cannot stall the reveal.
        /// </summary>
        private void TickReveal()
        {
            _openingWaitSeconds += Time.unscaledDeltaTime;

            if (_openingWaitSeconds < graphicsDelaySeconds) return;

            _settledBallCount = CountBalls();
            _active = true;
            Render();
        }

        private void OnBalanceClicked(PathType side)
        {
            if (!_active || _exiting || _pendingTap || _busyObserved || _flow.IsDone) return;

            _pendingSide = side;
            _pendingTap = true;
        }

        private void OnPointsAwarded()
        {
            if (!_active || _exiting || _flow.Step != TutorialStep.Match) return;

            var after = CountBalls();
            var cleared = _ballsBeforeTap + 1 - after;
            Debug.Log($"[Tutorial] clear detected: before={_ballsBeforeTap} after={after} cleared={cleared}");
            Handle(_flow.OnClear(cleared));
        }

        /// <summary>
        /// Overflow risk is a retry cue, never a defeat: the board shakes and the overlay
        /// shows the danger hint, but the overflow itself still routes through
        /// <see cref="TutorialFlow.OnOverflow"/> to the retry prompt.
        /// </summary>
        private void OnRiskChanged(BalanceRisk risk)
        {
            if (!_active) return;
            overlay.ShowDanger(risk != BalanceRisk.Safe);
        }

        private void OnSkip()
        {
            if (!_active || _exiting) return;
            Handle(_flow.OnSkip());
        }

        private void OnContinue()
        {
            if (!_active || _exiting) return;
            Handle(_flow.OnClose());
        }

        private void OnRetry()
        {
            if (!_active || _exiting) return;
            _exiting = true;

            TutorialResume.Step = (int)_retryStep;
            Storage.SaveDataGlobal(GlobalData.TutorialMode, "1");
            Debug.Log($"[Tutorial] retry -> reload Game at step {_retryStep}");
            ServiceLocator.Instance.GetService<ITransitionService>().ShowSplashScreen(() =>
                SceneManager.LoadScene("Game"));
        }

        private void Handle(FlowResult result)
        {
            if (result.IsDone)
            {
                Finish();
                return;
            }

            if (result.NeedsRetry)
            {
                _retryOffered = true;
                _retryStep = result.RetryStep;
                Debug.Log($"[Tutorial] retry offered for step {_retryStep}");
                overlay.ShowRetry();
                return;
            }

            if (result.Changed) Render();
        }

        private void Render()
        {
            overlay.ShowStep(_flow.Step, _flow.ExpectedSide);
        }

        /// <summary>Complete or skip: one durable write, then the existing post-game route.</summary>
        private void Finish()
        {
            if (_exiting) return;
            _exiting = true;

            Storage.SaveDataGlobal(GlobalData.TutorialCompleted, "true");
            Storage.FlushGlobal();
            TutorialResume.Clear();
            Debug.Log("[Tutorial] completed -> BallPass");

            if (_gameManager != null) _gameManager.RestoreTutorialOpponentHud();
            overlay.Hide();
            ServiceLocator.Instance.GetService<ITransitionService>().ShowSplashScreen(() =>
                SceneManager.LoadScene("BallPass"));
        }

        private int CountBalls()
        {
            return _balance.GetLeftPath().GetOnlyBalls() + _balance.GetRightPath().GetOnlyBalls();
        }

        private void OnDestroy()
        {
            if (_balance == null) return;

            _balance.OnBalanceClicked -= OnBalanceClicked;
            _balance.OnGetPoints -= OnPointsAwarded;
            _balance.RiskChanged -= OnRiskChanged;
        }
    }
}
