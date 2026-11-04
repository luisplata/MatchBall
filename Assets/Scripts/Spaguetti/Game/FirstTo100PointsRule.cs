namespace Spaguetti.Game
{
    public class FirstTo100PointsRule : IWinCondition
    {
        private const int TargetPoints = 100;
        private IGameWinConditionContext _context;

        public string GetDescription()
        {
            return "First to 100 points win";
        }

        public void SetContext(IGameWinConditionContext context)
        {
            _context = context;
        }

        public void OnFrameUpdate()
        {
            // This rule doesn't need per-frame updates
        }

        public WinResult CheckWinCondition(IPlayer player1, IPlayer player2)
        {
            int player1Coins = player1.GetCoins();
            int player2Coins = player2.GetCoins();

            if (player1Coins >= TargetPoints)
            {
                return new WinResult(player1, player2);
            }

            if (player2Coins >= TargetPoints)
            {
                return new WinResult(player2, player1);
            }

            return WinResult.None;
        }
    }
}