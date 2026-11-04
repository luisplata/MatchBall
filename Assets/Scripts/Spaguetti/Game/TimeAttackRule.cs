namespace Spaguetti.Game
{
    using UnityEngine;

    /// <summary>
    /// Time Attack Mode: Player with the most points after 3 minutes (180 seconds) wins.
    /// If the time runs out and both have equal points, they tie (can be extended to sudden death).
    /// Also respects the FullBalanceCondition: if balance fails, that takes precedence.
    /// </summary>
    public class TimeAttackRule : IWinCondition
    {
        private readonly float _matchDurationSeconds = 60f; // 30 seconds for quick testing, can be set to 180f for 3 minutes
        private IGameWinConditionContext _context;

        public string GetDescription()
        {
            return "Time Attack (60 seg): Player with the most points when time runs out wins.";
        }

        public void SetContext(IGameWinConditionContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Elapsed time is owned by the game context (IGameWinConditionContext.GetElapsedGameTime()).
        /// </summary>
        public void OnFrameUpdate()
        {
        }

        public WinResult CheckWinCondition(IPlayer player1, IPlayer player2)
        {
            // Time's up?
            if (_context.GetElapsedGameTime() >= _matchDurationSeconds)
            {
                // Time expired - whoever has more points wins
                int player1Coins = player1.GetCoins();
                int player2Coins = player2.GetCoins();

                if (player1Coins > player2Coins)
                {
                    return new WinResult(player1, player2);
                }
                else if (player2Coins > player1Coins)
                {
                    return new WinResult(player2, player1);
                }
                else
                {
                    // Tie: player1 wins by default (or could implement sudden death)
                    return new WinResult(player1, player2);
                }
            }

            return WinResult.None;
        }

        public float GetRemainingTime()
        {
            return Mathf.Max(0f, _matchDurationSeconds - _context.GetElapsedGameTime());
        }

        public bool IsTimeExpired()
        {
            return _context.GetElapsedGameTime() >= _matchDurationSeconds;
        }
    }
}

