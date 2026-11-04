namespace Spaguetti.Game
{
    /// <summary>
    /// Context interface that win conditions can use to query game state and timing.
    /// This decouples win condition logic from GameManager's internal structure.
    /// </summary>
    public interface IGameWinConditionContext
    {
        /// <summary>
        /// Get total elapsed time since game started (for timer-based conditions).
        /// </summary>
        float GetElapsedGameTime();

        /// <summary>
        /// Check if both players are ready to play.
        /// </summary>
        bool ArePlayersReady();
    }
}

