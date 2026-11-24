using System;
using System.Collections.Generic;
using Domain.Services;

namespace Domain.Ai
{
    /// <summary>
    /// The power-up kinds the rival policy understands. The adapter maps concrete power-up
    /// classes to these ids, so the policy never has to know a MonoBehaviour exists.
    /// </summary>
    public static class AiPowerUpKind
    {
        public const string Ray = "ray";
        public const string Ice = "ice";
        public const string Joker = "joker";
        public const string Lead = "lead";
        public const string Shell = "shell";
        public const string Unknown = "unknown";
    }

    /// <summary>
    /// One lane as the player can see it: how many balls it holds, where its next free slot is,
    /// and how many balls of each colour it carries. Pure data, no engine types.
    /// </summary>
    public sealed class AiLane
    {
        private static readonly IReadOnlyDictionary<string, int> NoColors =
            new Dictionary<string, int>();

        public AiLane(int balls, int nextFree, IReadOnlyDictionary<string, int> colorCounts)
        {
            Balls = balls;
            NextFree = nextFree;
            ColorCounts = colorCounts ?? NoColors;
        }

        /// <summary>Balls only: counter-balance weights are not balls and never counted.</summary>
        public int Balls { get; }

        public int NextFree { get; }
        public IReadOnlyDictionary<string, int> ColorCounts { get; }

        public int CountOf(string color)
        {
            if (string.IsNullOrEmpty(color)) return 0;
            return ColorCounts.TryGetValue(color, out var count) ? count : 0;
        }
    }

    /// <summary>A power-up the rival could click, addressed by its slot in the lane.</summary>
    public sealed class AiPowerUpOption
    {
        public AiPowerUpOption(int index, string kind)
        {
            Index = index;
            Kind = string.IsNullOrEmpty(kind) ? AiPowerUpKind.Unknown : kind;
        }

        public int Index { get; }
        public string Kind { get; }
    }

    /// <summary>
    /// Everything the policy is allowed to know: only what the player can see on screen.
    /// The feed beyond the ball already in the source zone is deliberately absent.
    /// </summary>
    public sealed class AiBoardSnapshot
    {
        public AiBoardSnapshot(AiLane left, AiLane right, int slots, string incomingColor,
            IReadOnlyList<AiPowerUpOption> powerUps)
        {
            Left = left ?? throw new ArgumentNullException(nameof(left));
            Right = right ?? throw new ArgumentNullException(nameof(right));
            Slots = Math.Max(1, slots);
            IncomingColor = incomingColor ?? string.Empty;
            PowerUps = powerUps ?? Array.Empty<AiPowerUpOption>();
        }

        public AiLane Left { get; }
        public AiLane Right { get; }
        public int Slots { get; }
        public string IncomingColor { get; }
        public IReadOnlyList<AiPowerUpOption> PowerUps { get; }

        /// <summary>Moves until either lane overflows, as the player's gauge computes it.</summary>
        public int MinCapacityMoves
        {
            get
            {
                return Math.Min(
                    BalanceRiskPolicy.CapacityMoves(Left.NextFree, Slots),
                    BalanceRiskPolicy.CapacityMoves(Right.NextFree, Slots));
            }
        }

        /// <summary>The same risk level the player's gauge shows for this board.</summary>
        public BalanceRisk Risk
        {
            get
            {
                return BalanceRiskPolicy.Evaluate(
                    Left.NextFree, Right.NextFree, Left.Balls, Right.Balls, Slots);
            }
        }
    }
}
