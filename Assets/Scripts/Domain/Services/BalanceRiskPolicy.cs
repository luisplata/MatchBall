using System;

namespace Domain.Services
{
    /// <summary>
    /// Graded danger of a settled board, measured in player moves before either loss
    /// path (capacity overflow or imbalance). <see cref="Safe"/> means three or more
    /// moves away, <see cref="Mild"/> two, <see cref="Strong"/> one.
    /// </summary>
    public enum BalanceRisk
    {
        Safe,
        Mild,
        Strong
    }

    /// <summary>
    /// The single source of truth for the near-loss distance. Pure functions of the
    /// settled board: capacity uses each path's next-free index (never a free-slot
    /// count) and imbalance uses the ball-only difference, so counter-balance never
    /// counts. Deliberately free of <c>UnityEngine</c> so it stays a pure function of
    /// its inputs for a later boundary test.
    /// </summary>
    public static class BalanceRiskPolicy
    {
        /// <summary>A loss is registered when the imbalance is strictly greater than this.</summary>
        public const int MaxImbalance = 3;

        /// <summary>
        /// Moves until the capacity path loses, from the settled next-free index. The loss
        /// path is the pre-tap <see cref="Path.HaveSpace"/> guard in <c>Balance.TryBall</c>
        /// (which requires <c>nextFree &lt; slots</c> on BOTH paths), not
        /// <c>IsTheLastPositionFulled()</c>: once a path's next free index reaches
        /// <c>slots</c>, the next tap is refused and that tap loses. Each accepted placement
        /// advances the settled next-free index one step on both paths — a ball is placed,
        /// then <c>BalanceBothPaths()</c> shifts the heavier/tapped path down one and the
        /// other up one — so the distance is <c>slots - nextFree + 1</c> (the trailing move is
        /// the losing tap). This is the no-clear worst case; a clear frees slots and extends
        /// life, so the distance is recomputed every settle.
        /// </summary>
        public static int CapacityMoves(int nextFree, int slots)
        {
            return Math.Max(1, slots - nextFree + 1);
        }

        /// <summary>
        /// Moves until the imbalance path loses, from the ball-only counts. The heavy
        /// side widens the gap by one per tap, so it is <c>MaxImbalance - abs(l - r) + 1</c>,
        /// floored at one move.
        /// </summary>
        public static int ImbalanceMoves(int l, int r)
        {
            return Math.Max(1, MaxImbalance - Math.Abs(l - r) + 1);
        }

        /// <summary>Maps a move count to its risk level; the closer path wins at <see cref="Evaluate"/>.</summary>
        public static BalanceRisk Level(int moves)
        {
            if (moves <= 1) return BalanceRisk.Strong;
            if (moves == 2) return BalanceRisk.Mild;
            return BalanceRisk.Safe;
        }

        /// <summary>
        /// Combined risk of a settled board: the minimum of both paths' moves until loss,
        /// so the nearer danger (and therefore <see cref="BalanceRisk.Strong"/>) wins.
        /// </summary>
        public static BalanceRisk Evaluate(int lNext, int rNext, int lBalls, int rBalls, int slots)
        {
            var capacityMoves = Math.Min(CapacityMoves(lNext, slots), CapacityMoves(rNext, slots));
            var imbalanceMoves = ImbalanceMoves(lBalls, rBalls);
            return Level(Math.Min(capacityMoves, imbalanceMoves));
        }
    }
}
