using System;
using App.Ports;
using App.Reward;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Infra.Reward
{
    /// <summary>
    /// Gold reward. A "gold" body is multiplied by <see cref="GoldMultiplier" /> exactly once, in
    /// <see cref="ResolveAmount" />, and both the label and the grant read it from there: the row
    /// advertises the amount the player actually receives (100 in the table shows and pays 1000).
    /// </summary>
    public sealed class GoldRewardHandler : IRewardHandler
    {
        private const string GoldIconKey = "gold";
        private const string GoldCurrencyId = "GOLD";
        private const int GoldMultiplier = 10;

        private readonly IInventoryRepository _inventoryRepository;

        public GoldRewardHandler(IInventoryRepository inventoryRepository)
        {
            _inventoryRepository = inventoryRepository;
        }

        public string TypeId => "gold";

        public RewardDisplayData Describe(string body)
        {
            if (!TryReadAmount(body, out var amount))
            {
                Debug.LogWarning($"[GoldRewardHandler] Reward body '{body}' has no usable amount; using the neutral label.");
                return new RewardDisplayData(TypeId, TypeId);
            }

            return new RewardDisplayData(GoldIconKey, $"{ResolveAmount(amount)} Gold");
        }

        public async UniTask<RewardGrantOutcome> GrantAsync(string body)
        {
            if (!TryReadAmount(body, out var amount))
            {
                Debug.LogWarning($"[GoldRewardHandler] Reward body '{body}' has no usable amount; grant skipped.");
                return RewardGrantOutcome.Skipped;
            }

            await _inventoryRepository.IncrementCurrency(GoldCurrencyId, ResolveAmount(amount));
            return RewardGrantOutcome.Granted;
        }

        /// <summary>
        /// The single place the table amount becomes the real payout. <see cref="Describe" /> and
        /// <see cref="GrantAsync" /> must both go through it; that is what keeps the row honest.
        /// </summary>
        private static int ResolveAmount(int amount)
        {
            return amount * GoldMultiplier;
        }

        private static bool TryReadAmount(string body, out int amount)
        {
            amount = 0;
            if (string.IsNullOrWhiteSpace(body)) return false;

            try
            {
                amount = JObject.Parse(body).Value<int?>("amount") ?? 0;
            }
            catch (Exception)
            {
                return false;
            }

            return amount > 0;
        }
    }
}
