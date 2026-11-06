using Cysharp.Threading.Tasks;

namespace App.Reward
{
    /// <summary>
    /// Registry that answers both questions about a reward: can it be granted, and how is it shown.
    /// </summary>
    public interface IRewardCatalog
    {
        /// <summary>True when a handler is registered for the persisted type string.</summary>
        bool Supports(string typeId);

        /// <summary>Display metadata for the row. Unknown types get a neutral fallback, never an exception.</summary>
        RewardDisplayData Describe(string typeId, string body);

        /// <summary>
        /// Grants the reward. Unknown types and malformed bodies are skipped, handler failures are
        /// reported as <see cref="RewardGrantOutcome.Failed" />; this never throws at the player.
        /// </summary>
        UniTask<RewardGrantOutcome> GrantAsync(string typeId, string body);
    }
}
