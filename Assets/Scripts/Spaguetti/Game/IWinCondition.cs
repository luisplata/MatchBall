namespace Spaguetti.Game
{
    public interface IWinCondition
    {
        string GetDescription();
        
        /// <summary>
        /// Set the game context that this condition will use to query timing/state.
        /// Called by GameManager during initialization.
        /// </summary>
        void SetContext(IGameWinConditionContext context);
        
        /// <summary>
        /// Called every frame BEFORE CheckWinCondition, allowing time-based conditions to update.
        /// </summary>
        void OnFrameUpdate();
        
        /// <summary>
        /// Evaluates if the win condition is met.
        /// Returns WinResult with winner/loser if condition is met, WinResult.None otherwise.
        /// </summary>
        WinResult CheckWinCondition(IPlayer player1, IPlayer player2);
    }
}