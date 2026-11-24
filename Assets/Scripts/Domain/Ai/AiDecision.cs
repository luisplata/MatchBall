namespace Domain.Ai
{
    public enum AiActionKind
    {
        TapLeft,
        TapRight,
        UsePowerUp
    }

    /// <summary>One legal move. <see cref="PowerUpIndex"/> is only meaningful for power-ups.</summary>
    public readonly struct AiAction
    {
        private AiAction(AiActionKind kind, int powerUpIndex)
        {
            Kind = kind;
            PowerUpIndex = powerUpIndex;
        }

        public static AiAction TapLeft => new AiAction(AiActionKind.TapLeft, -1);
        public static AiAction TapRight => new AiAction(AiActionKind.TapRight, -1);

        public static AiAction PowerUp(int index)
        {
            return new AiAction(AiActionKind.UsePowerUp, index);
        }

        public AiActionKind Kind { get; }
        public int PowerUpIndex { get; }
    }

    /// <summary>The chosen action, its score, and a one-line reason for the dev log.</summary>
    public readonly struct AiDecision
    {
        public AiDecision(AiAction action, int score, string reason)
        {
            Action = action;
            Score = score;
            Reason = reason ?? string.Empty;
        }

        public AiAction Action { get; }
        public int Score { get; }
        public string Reason { get; }
    }
}
