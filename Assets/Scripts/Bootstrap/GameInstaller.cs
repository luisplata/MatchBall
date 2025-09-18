using App.Ports;
using App.UseCase;
using Infra.EventBus;
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
            var memoryEventBus = new MemoryEventBus();
            ServiceLocator.Instance.RegisterService<IListStoreItemsUseCase>(new ListStoreItemsUseCase(storeRepository));
            ServiceLocator.Instance.RegisterService<IBuyItemUseCase>(new BuyItemUseCase(storeRepository,
                storeRepository, memoryEventBus));
            ServiceLocator.Instance.RegisterService<IEventBus>(memoryEventBus);
        }
    }
}