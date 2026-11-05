namespace Presentation.Tutorial
{
    /// <summary>
    /// Ordered curriculum. The numeric value is the resume contract carried by
    /// <see cref="TutorialResume"/> across the scene reload that Retry performs.
    /// </summary>
    public enum TutorialStep
    {
        Drop = 0,
        Match = 1,
        Balance = 2,
        Close = 3,
        Done = 4
    }

    /// <summary>Outcome of one transition. Immutable: the caller reacts, never mutates it.</summary>
    public readonly struct FlowResult
    {
        public readonly bool Changed;
        public readonly bool NeedsRetry;
        public readonly TutorialStep RetryStep;
        public readonly bool IsDone;

        public FlowResult(bool changed, bool needsRetry, TutorialStep retryStep, bool isDone)
        {
            Changed = changed;
            NeedsRetry = needsRetry;
            RetryStep = retryStep;
            IsDone = isDone;
        }

        /// <summary>Nothing to do: the event was not accepted in the current step.</summary>
        public static FlowResult Ignored => new FlowResult(false, false, TutorialStep.Done, false);

        /// <summary>The step or its hint moved: the overlay must redraw.</summary>
        public static FlowResult Redraw => new FlowResult(true, false, TutorialStep.Done, false);

        public static FlowResult Retry(TutorialStep step) => new FlowResult(true, true, step, false);

        public static FlowResult Finish() => new FlowResult(true, false, TutorialStep.Done, true);
    }

    /// <summary>
    /// The whole tutorial curriculum as a pure state machine: no UnityEngine, no timers, no I/O.
    /// The controller feeds it accepted taps and board events and acts on the returned result.
    /// The first step advances on the first accepted tap; the match step is gated on balls
    /// cleared (never on points, and never on the expected side being the one tapped).
    /// </summary>
    public sealed class TutorialFlow
    {
        /// <summary>A clear of this many balls advances the match step.</summary>
        public const int BallsToClear = 3;

        /// <summary>Accepted taps needed to leave the balance step.</summary>
        public const int TapsToClose = 3;

        /// <summary>
        /// Scripted taps that build the seeded 3+ match: blue/red/blue stacked on the left against
        /// red/red/blue on the right. Indexed by <see cref="TapsMade"/>.
        /// </summary>
        private static readonly PathType[] Plan =
        {
            PathType.Left, PathType.Right,
            PathType.Left, PathType.Right,
            PathType.Left, PathType.Right
        };

        private TutorialStep _step;
        private int _tapsMade;
        private int _balanceTaps;
        private bool _awaitingClear;

        public TutorialFlow(int resumeStep = 0)
        {
            _step = NormalizeStep(resumeStep);
        }

        public TutorialStep Step => _step;
        public int TapsMade => _tapsMade;
        public int BalanceTaps => _balanceTaps;
        public PathType LastTap { get; private set; } = PathType.None;
        public bool IsDone => _step == TutorialStep.Done;
        public bool PlanExhausted => _tapsMade >= Plan.Length;

        /// <summary>Side the overlay should highlight; <see cref="PathType.None"/> hides both hints.</summary>
        public PathType ExpectedSide
        {
            get
            {
                if (_step != TutorialStep.Drop && _step != TutorialStep.Match) return PathType.None;
                if (_tapsMade >= Plan.Length) return PathType.None;
                return Plan[_tapsMade];
            }
        }

        /// <summary>An accepted tap on a path. Rejected taps never reach the flow.</summary>
        public FlowResult OnTap(PathType side)
        {
            switch (_step)
            {
                case TutorialStep.Drop:
                    LastTap = side;
                    _tapsMade++;
                    _step = TutorialStep.Match;
                    return FlowResult.Redraw;

                case TutorialStep.Match:
                    if (_tapsMade >= Plan.Length) return FlowResult.Ignored;
                    LastTap = side;
                    _tapsMade++;
                    // The sixth ball's match resolves asynchronously: never retry before it gets
                    // its chance, or the tutorial would abort the very match it is teaching.
                    _awaitingClear = _tapsMade >= Plan.Length;
                    return FlowResult.Redraw;

                case TutorialStep.Balance:
                    LastTap = side;
                    _balanceTaps++;
                    if (_balanceTaps < TapsToClose) return FlowResult.Redraw;
                    _step = TutorialStep.Close;
                    return FlowResult.Redraw;

                default:
                    return FlowResult.Ignored;
            }
        }

        /// <summary>How many balls the board actually removed. Anything under three is not a lesson.</summary>
        public FlowResult OnClear(int cleared)
        {
            if (_step != TutorialStep.Match) return FlowResult.Ignored;

            _awaitingClear = false;
            if (cleared < BallsToClear) return FlowResult.Ignored;

            _step = TutorialStep.Balance;
            _balanceTaps = 0;
            return FlowResult.Redraw;
        }

        /// <summary>
        /// Called once the last queued tap has fully settled without a board event. Only the
        /// plan-exhausted guard uses it: six drops with no match means the board must be replayed.
        /// </summary>
        public FlowResult OnSettled()
        {
            if (!_awaitingClear) return FlowResult.Ignored;

            _awaitingClear = false;
            return _step == TutorialStep.Match ? FlowResult.Retry(TutorialStep.Match) : FlowResult.Ignored;
        }

        /// <summary>Overflow always offers a retry of the current step, never a real game over.</summary>
        public FlowResult OnOverflow()
        {
            if (_step == TutorialStep.Done || _step == TutorialStep.Close) return FlowResult.Ignored;
            return FlowResult.Retry(_step);
        }

        public FlowResult OnSkip()
        {
            _step = TutorialStep.Done;
            return FlowResult.Finish();
        }

        /// <summary>Continue on the closing card. Only meaningful while the card is showing.</summary>
        public FlowResult OnClose()
        {
            if (_step != TutorialStep.Close) return FlowResult.Ignored;

            _step = TutorialStep.Done;
            return FlowResult.Finish();
        }

        private static TutorialStep NormalizeStep(int resumeStep)
        {
            if (resumeStep <= (int)TutorialStep.Drop) return TutorialStep.Drop;
            if (resumeStep >= (int)TutorialStep.Done) return TutorialStep.Close;
            return (TutorialStep)resumeStep;
        }
    }
}
