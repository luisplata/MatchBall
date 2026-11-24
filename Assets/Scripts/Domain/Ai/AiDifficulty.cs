namespace Domain.Ai
{
    /// <summary>
    /// How sharp the rival plays. The only thing a level changes is how often it misreads the
    /// board: 0 is the exact best action, 1 is a coin flip over the legal ones.
    /// <para>
    /// <see cref="Default"/> exists because a freshly added serialized enum can deserialize as 0;
    /// mapping it to the Hard profile keeps a scene that never set the field from falling to Easy.
    /// </para>
    /// </summary>
    public enum AiDifficulty
    {
        Default,
        Easy,
        Normal,
        Hard,
        Perfect
    }

    public static class AiDifficultyProfile
    {
        public static float Noise(AiDifficulty level)
        {
            switch (level)
            {
                case AiDifficulty.Easy: return 0.55f;
                case AiDifficulty.Normal: return 0.25f;
                case AiDifficulty.Default:
                case AiDifficulty.Hard: return 0.10f;
                default: return 0f;
            }
        }

        /// <summary>
        /// Seconds between decisions. Higher levels think faster, but the value is pacing, not
        /// fairness: each board fills on its own, so a slower rival only hurts itself.
        /// </summary>
        public static float CadenceSeconds(AiDifficulty level)
        {
            switch (level)
            {
                case AiDifficulty.Easy: return 4f;
                case AiDifficulty.Normal: return 3f;
                case AiDifficulty.Default:
                case AiDifficulty.Hard: return 2.5f;
                default: return 2f;
            }
        }
    }
}
