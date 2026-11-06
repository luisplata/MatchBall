using System;

namespace Domain.Services
{
    /// <summary>
    /// Pure policy for the per-board placement deadline: maps the time left on a human
    /// board to a <see cref="BalanceRisk"/> and picks the side a stalled ball is auto-placed
    /// on. Deliberately free of <c>UnityEngine</c> so both functions stay pure of their
    /// inputs for a later boundary test.
    /// </summary>
    public static class PlacementDeadlinePolicy
    {
        /// <summary>
        /// Remaining-fraction thresholds. Above <see cref="MildRemainingFraction"/> is
        /// <see cref="BalanceRisk.Safe"/>, above <see cref="StrongRemainingFraction"/> is
        /// <see cref="BalanceRisk.Mild"/>, anything at or below it is
        /// <see cref="BalanceRisk.Strong"/>.
        /// </summary>
        public const float MildRemainingFraction = 0.50f;
        public const float StrongRemainingFraction = 0.20f;

        /// <summary>
        /// Maps the remaining time to a graded risk. A non-positive limit is a degenerate
        /// board and always reads <see cref="BalanceRisk.Strong"/>.
        /// </summary>
        public static BalanceRisk Risk(float remainingSeconds, float limitSeconds)
        {
            if (limitSeconds <= 0f) return BalanceRisk.Strong;

            var fraction = remainingSeconds / limitSeconds;
            if (fraction > MildRemainingFraction) return BalanceRisk.Safe;
            if (fraction > StrongRemainingFraction) return BalanceRisk.Mild;
            return BalanceRisk.Strong;
        }

        /// <summary>
        /// Uniform 50/50 side for an expiry auto-placement. Never returns
        /// <see cref="PathType.None"/> and never prefers the safer side: the choice is
        /// deliberately independent of board risk.
        /// </summary>
        public static PathType ExpirySide(Random random)
        {
            return random.Next(2) == 0 ? PathType.Left : PathType.Right;
        }
    }
}
