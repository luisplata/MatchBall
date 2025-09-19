using App.Ports;
using App.UseCase;
using App.UseCase.GameConfiguration;
using App.UseCase.PowerUps;
using Infra.EventBus;
using Infra.UnityAdapters;
using Infra.UnityConfiguration;
using Infrastructure.UnityEconomy;
using UnityEngine;

namespace Bootstrap
{
    public class GameInstaller : MonoBehaviour
    {
        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            var storeRepository = new UnityEconomyStoreRepository();
            var memoryEventBus = new MemoryEventBus();//IEventBus
            var storageRepository = new SaveDataRepository();
            var gameConfigurationAdapter = new UnityConfigurationAdapter();
            
            ServiceLocator.Instance.RegisterService<IEventBus>(memoryEventBus);

            ServiceLocator.Instance.RegisterService<IListStoreItemsUseCase>(new ListStoreItemsUseCase(storeRepository));
            ServiceLocator.Instance.RegisterService<IBuyItemUseCase>(new BuyItemUseCase(storeRepository,
                storeRepository, memoryEventBus));
            ServiceLocator.Instance.RegisterService<IGameConfigurationUseCase>(
                new GameConfigurationUseCase(gameConfigurationAdapter, storageRepository));
            ServiceLocator.Instance.RegisterService<IPowerUpsUseCase>(new PowerUpsUseCase(storeRepository, memoryEventBus));
        }
    }
}