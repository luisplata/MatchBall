namespace Spaguetti.Game
{
    /// <summary>
    /// Represents the outcome of a win condition check.
    /// Returns who won and who lost when a condition is met.
    /// </summary>
    public struct WinResult
    {
        public IPlayer Winner { get; }
        public IPlayer Loser { get; }
        public bool IsValid { get; }

        public WinResult(IPlayer winner, IPlayer loser)
        {
            Winner = winner;
            Loser = loser;
            IsValid = winner != null && loser != null && winner != loser;
        }

        /// <summary>
        /// Creates an invalid/null result (no winner yet).
        /// </summary>
        public static WinResult None => new(null, null);
    }
}

