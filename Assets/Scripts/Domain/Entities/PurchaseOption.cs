using System.Collections.Generic;

namespace Domain.Model
{
    public class PurchaseOption
    {
        public string PurchaseId { get; }
        public string DisplayName { get; }
        public string ImageKey { get; }
        public List<(string currencyId, int amount)> Costs { get; }
        public (string identify, string rewardName, string type, int quantity) Reward { get; }

        public PurchaseOption(
            string purchaseId,
            string displayName,
            string imageKey,
            List<(string, int)> costs,
            (string, string, string, int) reward)
        {
            PurchaseId = purchaseId;
            DisplayName = displayName;
            ImageKey = imageKey;
            Costs = costs;
            Reward = reward;
        }
    }
}