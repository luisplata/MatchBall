namespace Presentation.Tutorial
{
    /// <summary>
    /// Carries the interrupted tutorial step across the scene reload that Retry performs.
    /// Process-local on purpose: there is no board-reset API, so a retry reloads <c>Game</c>,
    /// and the durable flag must stay untouched by that reload.
    /// </summary>
    public static class TutorialResume
    {
        public static int Step;

        public static void Clear()
        {
            Step = 0;
        }
    }
}
