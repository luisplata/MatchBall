using Cysharp.Threading.Tasks;

namespace App.Reward
{
    /// <summary>
    /// One reward kind. A handler both describes the row (icon key + label) and grants it,
    /// so display and grant can never disagree about what is supported.
    /// </summary>
    public interface IRewardHandler
    {
        /// <summary>Persisted type string, matched exactly, e.g. "gold".</summary>
        string TypeId { get; }

        /// <summary>Display metadata for a row. Must never throw.</summary>
        RewardDisplayData Describe(string body);

        /// <summary>Grants the reward. Must not throw; report the outcome instead.</summary>
        UniTask<RewardGrantOutcome> GrantAsync(string body);
    }
}
