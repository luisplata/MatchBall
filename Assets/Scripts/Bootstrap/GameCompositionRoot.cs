using System;
using App.Ports;
using App.Reward;
using App.UseCase;
using App.UseCase.GameConfiguration;
using App.UseCase.PassBall;
using App.UseCase.PowerUps;
using App.UseCase.Reward;
using Infra.AdModAdapter;
using Infra.EventBus;
using Infra.Persistence;
using Infra.Reward;
using Infra.UnityAdapters;
using Infra.UnityConfiguration;
using Infra.WebAdapter;
using Infrastructure.UnityEconomy;
using UnityEngine;

namespace Bootstrap
{
    public static class GameCompositionRoot
    {
        public static void Install(string url, string uri)
        {
            var storeRepository = new EconomyLocal(); //IStoreRepository, IInventoryRepository
            var memoryEventBus = new MemoryEventBus(); //IEventBus
            var storageRepository = new SaveDataRepository();
            var gameConfigurationAdapter = new UnityConfigurationAdapter();
            var webAdapter = new WebAdapter(url, uri);
            var adModAdapter = new FakeAdModAdapter();

            ServiceLocator.Instance.RegisterService<IEventBus>(memoryEventBus);
            
            ServiceLocator.Instance.RegisterService<IInventoryRepository>(storeRepository);

            ServiceLocator.Instance.RegisterService<IListStoreItemsUseCase>(new ListStoreItemsUseCase(storeRepository));
            ServiceLocator.Instance.RegisterService<IBuyItemUseCase>(new BuyItemUseCase(storeRepository,
                storeRepository, memoryEventBus));
            ServiceLocator.Instance.RegisterService<IGameConfigurationUseCase>(
                new GameConfigurationUseCase(gameConfigurationAdapter, storageRepository));
            ServiceLocator.Instance.RegisterService<IPowerUpsUseCase>(new PowerUpsUseCase(storeRepository,
                memoryEventBus));
            ServiceLocator.Instance.RegisterService<IClaimGoldUseCase>(new ClaimGoldUseCase(storeRepository,
                memoryEventBus));
            var claimPowerUpUseCase = new ClaimPowerUpUseCase(storeRepository);
            ServiceLocator.Instance.RegisterService<IClaimPowerUpUseCase>(claimPowerUpUseCase);

            var rewardCatalog = RewardCatalogFactory.Create(storeRepository, claimPowerUpUseCase);
            foreach (var issue in rewardCatalog.ValidateTable(BallPassLocalStore.RewardTable))
            {
                Debug.LogError($"[RewardCatalog] {issue}");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                throw new InvalidOperationException($"[RewardCatalog] Invalid ball pass reward table: {issue}");
#endif
            }

            ServiceLocator.Instance.RegisterService<IRewardCatalog>(rewardCatalog);

            ServiceLocator.Instance.RegisterService<IPassBallUseCase>(new PassBallUseCase(webAdapter));
            ServiceLocator.Instance.RegisterService<ILoginUseCase>(new LoginUseCase(webAdapter, adModAdapter));
            ServiceLocator.Instance.RegisterService<IAdModUseCase>(new AdModUseCase(adModAdapter));
            ServiceLocator.Instance.RegisterService<IWebAdapter>(webAdapter);
        }
    }
}
