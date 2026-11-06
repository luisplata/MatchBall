using System;
using App.Ports;
using App.Reward;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Infra.Reward
{
    /// <summary>
    /// Gold reward. A "gold" body grants its amount times <see cref="GoldMultiplier" /> GOLD,
    /// which keeps the historical payout (100 in the table pays 1000) in one visible place.
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

            return new RewardDisplayData(GoldIconKey, $"{amount} Gold");
        }

        public async UniTask<RewardGrantOutcome> GrantAsync(string body)
        {
            if (!TryReadAmount(body, out var amount))
            {
                Debug.LogWarning($"[GoldRewardHandler] Reward body '{body}' has no usable amount; grant skipped.");
                return RewardGrantOutcome.Skipped;
            }

            await _inventoryRepository.IncrementCurrency(GoldCurrencyId, amount * GoldMultiplier);
            return RewardGrantOutcome.Granted;
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
