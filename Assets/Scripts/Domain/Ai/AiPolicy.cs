using System;
using System.Collections.Generic;
using Domain.Services;

namespace Domain.Ai
{
    /// <summary>
    /// Picks the rival's next action from a visible board snapshot.
    /// <para>
    /// Pure by contract: no engine types, no services, no scene. Taps are scored so the order is
    /// lexicographic — one step of remaining capacity outweighs every other term combined, so the
    /// rival defends the lane before it gets greedy.
    /// </para>
    /// </summary>
    public static class AiPolicy
    {
        /// <summary>One step of remaining capacity outranks payout and imbalance together.</summary>
        private const int CapacityWeight = 1000;

        /// <summary>
        /// Payout is the first tiebreak. The ceiling is <c>ScorePolicy.BasePoints(5) *
        /// ScorePolicy.MultiplierCap = 25</c>, so <c>25 * 20 = 500 &lt; 1000</c> and capacity still
        /// dominates even with a maximum streak.
        /// </summary>
        private const int PayoutWeight = 20;

        /// <summary>Imbalance distance is the last tiebreak: at most MaxImbalance + 1 = 4.</summary>
        private const int ImbalanceWeight = 1;

        /// <summary>
        /// Temperature of the softmax half of the blend. Small on purpose: at difficulty 0.3 the
        /// rival mostly plays the best action and occasionally misreads the board.
        /// </summary>
        private const float SoftmaxTemperature = 300f;

        /// <summary>Balls of one colour that clear.</summary>
        private const int MatchSize = 3;

        /// <summary>
        /// Clicking a power-up spends the turn, so it is only worth it when its relief buys back
        /// more than this many payout points. Below it, the rival taps instead of burning an orb.
        /// </summary>
        private const int PowerUpSkipCost = 2;

        public static AiDecision Decide(AiBoardSnapshot snapshot, float difficulty, int seed)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            var candidates = BuildCandidates(snapshot);
            var permissible = KeepImbalanceSafe(candidates, snapshot);

            if (difficulty <= 0f) return Best(permissible);
            return Sample(permissible, Math.Clamp(difficulty, 0f, 1f), seed);
        }

        private static List<AiDecision> BuildCandidates(AiBoardSnapshot snapshot)
        {
            var left = ScoreTap(snapshot, AiAction.TapLeft, snapshot.Left, snapshot.Right);
            var right = ScoreTap(snapshot, AiAction.TapRight, snapshot.Right, snapshot.Left);

            var candidates = new List<AiDecision>(2 + snapshot.PowerUps.Count) {left, right};
            var tapCeiling = Math.Max(left.Score, right.Score);

            // A feed or opponent power-up leaves the board untouched: scoring it on the current
            // capacity would always beat a tap that costs capacity, so it is priced against the
            // best tap plus the value of its relief. On a safe board the rival holds them.
            var underPressure = snapshot.Risk != BalanceRisk.Safe;

            for (var i = 0; i < snapshot.PowerUps.Count; i++)
            {
                var option = snapshot.PowerUps[i];
                var utility = UtilityOf(snapshot, option);
                if (utility <= 0) continue;
                // Held while the board is safe: only Ray is real board relief, and even that is
                // skipped when it has nothing to remove (utility 0 above).
                if (!underPressure && option.Kind != AiPowerUpKind.Ray) continue;

                candidates.Add(new AiDecision(
                    AiAction.PowerUp(option.Index),
                    tapCeiling + (utility - PowerUpSkipCost) * PayoutWeight,
                    $"{option.Kind} utility={utility}"));
            }

            return candidates;
        }

        private static AiDecision ScoreTap(AiBoardSnapshot snapshot, AiAction action, AiLane lane,
            AiLane other)
        {
            // Post-tap estimate: the tapped lane advances one slot and gains one ball. The real
            // BalanceBothPaths rebalance is not simulated, so this can only understate how good
            // the board ends up — never overstate it.
            var nextFreeAfter = lane.NextFree + 1;
            var ballsAfter = lane.Balls + 1;

            var capacity = Math.Min(
                BalanceRiskPolicy.CapacityMoves(nextFreeAfter, snapshot.Slots),
                BalanceRiskPolicy.CapacityMoves(other.NextFree, snapshot.Slots));

            var leftAfter = action.Kind == AiActionKind.TapLeft ? ballsAfter : other.Balls;
            var rightAfter = action.Kind == AiActionKind.TapRight ? ballsAfter : other.Balls;

            var payout = ProjectPayout(snapshot, lane);
            var imbalance = BalanceRiskPolicy.ImbalanceMoves(leftAfter, rightAfter);
            var score = capacity * CapacityWeight + payout * PayoutWeight +
                        imbalance * ImbalanceWeight;

            return new AiDecision(action, score,
                $"cap={capacity} payout={payout} imbalance={imbalance}");
        }

        private static int ProjectPayout(AiBoardSnapshot snapshot, AiLane lane)
        {
            if (string.IsNullOrEmpty(snapshot.IncomingColor)) return 0;

            var sameColor = lane.CountOf(snapshot.IncomingColor) + 1;
            return sameColor < MatchSize ? 0 : ScorePolicy.BasePoints(sameColor);
        }

        /// <summary>
        /// Expected relief of a power-up in payout units. Ray is the only one that changes the
        /// board, so its value is the number of balls its colour removal takes out; the rest act on
        /// the feed or the opponent and get a fixed, small value.
        /// </summary>
        private static int UtilityOf(AiBoardSnapshot snapshot, AiPowerUpOption option)
        {
            switch (option.Kind)
            {
                case AiPowerUpKind.Ray:
                    return RayRemoval(snapshot);
                case AiPowerUpKind.Ice:
                    return 3;
                case AiPowerUpKind.Joker:
                    return 2;
                case AiPowerUpKind.Lead:
                    return 1;
                case AiPowerUpKind.Shell:
                    return 1;
                default:
                    return 0;
            }
        }

        private static int RayRemoval(AiBoardSnapshot snapshot)
        {
            var color = MostCommonColor(snapshot);
            return snapshot.Left.CountOf(color) + snapshot.Right.CountOf(color);
        }

        private static string MostCommonColor(AiBoardSnapshot snapshot)
        {
            var best = string.Empty;
            var bestTotal = 0;

            foreach (var pair in snapshot.Left.ColorCounts)
            {
                var total = pair.Value + snapshot.Right.CountOf(pair.Key);
                if (total > bestTotal)
                {
                    bestTotal = total;
                    best = pair.Key;
                }
            }

            foreach (var pair in snapshot.Right.ColorCounts)
            {
                var total = pair.Value + snapshot.Left.CountOf(pair.Key);
                if (total > bestTotal)
                {
                    bestTotal = total;
                    best = pair.Key;
                }
            }

            return best;
        }

        /// <summary>
        /// Drops any tap that would lose on imbalance (MUST-12). When every candidate would, the
        /// board is already lost and the full set is returned so the rival still plays.
        /// </summary>
        private static List<AiDecision> KeepImbalanceSafe(List<AiDecision> candidates,
            AiBoardSnapshot snapshot)
        {
            var safe = new List<AiDecision>(candidates.Count);
            for (var i = 0; i < candidates.Count; i++)
            {
                if (!ExceedsImbalance(candidates[i].Action, snapshot)) safe.Add(candidates[i]);
            }

            return safe.Count > 0 ? safe : candidates;
        }

        private static bool ExceedsImbalance(AiAction action, AiBoardSnapshot snapshot)
        {
            if (action.Kind == AiActionKind.UsePowerUp) return false;

            var tappingLeft = action.Kind == AiActionKind.TapLeft;
            var ballsAfter = (tappingLeft ? snapshot.Left.Balls : snapshot.Right.Balls) + 1;
            var otherBalls = tappingLeft ? snapshot.Right.Balls : snapshot.Left.Balls;
            return Math.Abs(ballsAfter - otherBalls) > BalanceRiskPolicy.MaxImbalance;
        }

        private static AiDecision Best(List<AiDecision> candidates)
        {
            var best = candidates[0];
            for (var i = 1; i < candidates.Count; i++)
            {
                if (candidates[i].Score > best.Score) best = candidates[i];
            }

            return best;
        }

        /// <summary>
        /// Blends the sharp softmax with a uniform pick: <c>difficulty</c> is literally the weight
        /// of the uniform part, so 0 is the argmax and 1 is a coin flip over the legal actions.
        /// The best action's probability therefore decays monotonically as difficulty rises.
        /// </summary>
        private static AiDecision Sample(List<AiDecision> candidates, float difficulty, int seed)
        {
            var top = candidates[0].Score;
            for (var i = 1; i < candidates.Count; i++)
            {
                if (candidates[i].Score > top) top = candidates[i].Score;
            }

            var weights = new double[candidates.Count];
            var total = 0.0;
            for (var i = 0; i < candidates.Count; i++)
            {
                weights[i] = Math.Exp((candidates[i].Score - top) / SoftmaxTemperature);
                total += weights[i];
            }

            var uniform = 1.0 / candidates.Count;
            var pick = new Random(seed).NextDouble();
            var accumulated = 0.0;

            for (var i = 0; i < candidates.Count; i++)
            {
                var probability = (1.0 - difficulty) * (weights[i] / total) + difficulty * uniform;
                accumulated += probability;
                if (pick <= accumulated) return candidates[i];
            }

            return candidates[candidates.Count - 1];
        }
    }
}
