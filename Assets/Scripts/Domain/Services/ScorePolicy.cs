using System;

namespace Domain.Services
{
    /// <summary>
    /// Result of resolving one match. <see cref="Applied"/> is false when the
    /// request was a no-op, in which case callers MUST leave their state untouched.
    /// </summary>
    public readonly struct ScoreAward
    {
        public readonly int Points;
        public readonly int NextMultiplier;
        public readonly float NextAwardSeconds;
        public readonly bool Applied;

        public ScoreAward(int points, int nextMultiplier, float nextAwardSeconds, bool applied)
        {
            Points = points;
            NextMultiplier = nextMultiplier;
            NextAwardSeconds = nextAwardSeconds;
            Applied = applied;
        }

        public static ScoreAward None => new ScoreAward(0, 0, 0f, false);
    }

    /// <summary>
    /// The single source of truth for match scoring: base award by clear size and
    /// the multiplicative streak that decays with elapsed play time.
    /// Deliberately free of <c>UnityEngine</c> so it stays a pure function of its inputs.
    /// </summary>
    public static class ScorePolicy
    {
        public const int BaseCap = 5;
        public const int MultiplierCap = 5;
        public const float DecayWindowSeconds = 5f;
        public const float DecayStepSeconds = 3f;

        public static int BasePoints(int ballsCleared)
        {
            return Math.Min(BaseCap, Math.Max(1, ballsCleared - 1));
        }

        public static int Decay(int multiplier, float gapSeconds)
        {
            if (gapSeconds <= DecayWindowSeconds) return multiplier;
            var steps = 1 + (int)((gapSeconds - DecayWindowSeconds) / DecayStepSeconds);
            return Math.Max(1, multiplier - steps);
        }

        public static int Raise(int multiplier)
        {
            return Math.Min(MultiplierCap, multiplier + 1);
        }

        public static ScoreAward Award(int ballsCleared, float nowSeconds, float lastAwardAtSeconds, int multiplier)
        {
            // A no-op clear must not touch the decay clock or the streak.
            if (ballsCleared <= 0) return ScoreAward.None;

            var decayed = Decay(multiplier, nowSeconds - lastAwardAtSeconds);
            return new ScoreAward(BasePoints(ballsCleared) * decayed, Raise(decayed), nowSeconds, true);
        }
    }
}
