using Bootstrap;
using Commander;
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

        private TutorialFlow _flow;
        private GameManager _gameManager;
        private IPlayer _player;
        private Balance _balance;

        private bool _active;
        private bool _exiting;
        private bool _retryOffered;
        private bool _pendingTap;
        private bool _busyObserved;
        private PathType _pendingSide;
        private int _settledBallCount;
        private int _ballsBeforeTap;
        private TutorialStep _retryStep;

        private static IStorageData Storage => ServiceLocator.Instance.GetService<IStorageData>();

        private void Start()
        {
            _gameManager = FindFirstObjectByType<GameManager>();
            if (_gameManager == null || !_gameManager.IsTutorialMode())
            {
                overlay.Hide();
                enabled = false;
                return;
            }

            _player = _gameManager.GetFirstPlayer();
            _balance = _player.GetBalance() as Balance;
            if (_balance == null)
            {
                Debug.LogError("[Tutorial] the first player has no Balance; tutorial disabled.");
                overlay.Hide();
                enabled = false;
                return;
            }

            _flow = new TutorialFlow(TutorialResume.Step);
            _balance.OnBalanceClicked += OnBalanceClicked;
            _balance.OnGetPoints += OnPointsAwarded;
            overlay.Initialize(OnSkip, OnContinue, OnRetry);

            _settledBallCount = CountBalls();
            _active = true;
            Debug.Log($"[Tutorial] started at step {_flow.Step} (resume {TutorialResume.Step})");
            Render();
        }

        private void Update()
        {
            if (!_active || _exiting || _retryOffered) return;

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
        }
    }
}
