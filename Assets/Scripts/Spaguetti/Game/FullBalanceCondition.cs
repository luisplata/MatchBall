namespace Spaguetti.Game
{
    public class FullBalanceCondition : IWinCondition
    {
        public string GetDescription()
        {
            return "Full Balance: The player who reaches the full balance first wins.";
        }

        public void SetContext(IGameWinConditionContext context)
        {
            // This rule doesn't need game context
        }

        public void OnFrameUpdate()
        {
            // This rule doesn't need per-frame updates
        }

        public WinResult CheckWinCondition(IPlayer player1, IPlayer player2)
        {
            if (player1.IsGameOver())
            {
                return new WinResult(player2, player1);
            }

            if (player2.IsGameOver())
            {
                return new WinResult(player1, player2);
            }

            return WinResult.None;
        }
    }
}