namespace App.Reward
{
    /// <summary>Result of a grant attempt. Only <see cref="Granted" /> means the player was paid.</summary>
    public enum RewardGrantOutcome
    {
        /// <summary>The reward was granted.</summary>
        Granted,

        /// <summary>Nothing was granted on purpose: unknown kind or unusable body.</summary>
        Skipped,

        /// <summary>The handler was registered but the grant threw.</summary>
        Failed
    }
}
