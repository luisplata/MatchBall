using App.Ports;
using App.UseCase;
using App.UseCase.GameConfiguration;
using App.UseCase.PassBall;
using App.UseCase.PowerUps;
using App.UseCase.Reward;
using Infra.EventBus;
using Infra.UnityAdapters;
using Infra.UnityConfiguration;
using Infra.WebAdapter;
using Infrastructure.UnityEconomy;
using UnityEngine;

namespace Bootstrap
{
    public class GameInstaller : MonoBehaviour
    {
        [SerializeField] private string url, uri;

        private void Awake()
        {
            //TODO: Falta refactorizar el pase de batalla para darle manejo.
            DontDestroyOnLoad(gameObject);
            var storeRepository = new UnityEconomyStoreRepository(); //IStoreRepository, IInventoryRepository
            var memoryEventBus = new MemoryEventBus(); //IEventBus
            var storageRepository = new SaveDataRepository();
            var gameConfigurationAdapter = new UnityConfigurationAdapter();
            var webAdapter = new WebAdapter(url, uri);

            ServiceLocator.Instance.RegisterService<IEventBus>(memoryEventBus);

            ServiceLocator.Instance.RegisterService<IListStoreItemsUseCase>(new ListStoreItemsUseCase(storeRepository));
            ServiceLocator.Instance.RegisterService<IBuyItemUseCase>(new BuyItemUseCase(storeRepository,
                storeRepository, memoryEventBus));
            ServiceLocator.Instance.RegisterService<IGameConfigurationUseCase>(
                new GameConfigurationUseCase(gameConfigurationAdapter, storageRepository));
            ServiceLocator.Instance.RegisterService<IPowerUpsUseCase>(new PowerUpsUseCase(storeRepository,
                memoryEventBus));
            ServiceLocator.Instance.RegisterService<IClaimGoldUseCase>(new ClaimGoldUseCase(storeRepository,
                memoryEventBus));
            ServiceLocator.Instance.RegisterService<IClaimPowerUpUseCase>(new ClaimPowerUpUseCase(storeRepository));
            ServiceLocator.Instance.RegisterService<IPassBallUseCase>(new PassBallUseCase(webAdapter));
            ServiceLocator.Instance.RegisterService<ILoginUseCase>(new LoginUseCase(webAdapter));
        }
    }
}