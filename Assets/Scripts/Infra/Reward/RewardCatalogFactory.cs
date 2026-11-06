using System;
using App.Ports;
using App.Reward;
using App.UseCase.Reward;

namespace Infra.Reward
{
    /// <summary>
    /// The only place where reward kinds are wired. Adding a kind that grants and renders is one
    /// handler type plus one line in the array below; no switch anywhere else has to change.
    /// </summary>
    public static class RewardCatalogFactory
    {
        public static RewardCatalog Create(IInventoryRepository inventoryRepository,
            IClaimPowerUpUseCase claimPowerUpUseCase)
        {
            if (inventoryRepository == null) throw new ArgumentNullException(nameof(inventoryRepository));
            if (claimPowerUpUseCase == null) throw new ArgumentNullException(nameof(claimPowerUpUseCase));

            return new RewardCatalog(new IRewardHandler[]
            {
                new GoldRewardHandler(inventoryRepository),
                new PowerUpRewardHandler(claimPowerUpUseCase)
            });
        }
    }
}
