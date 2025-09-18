using App.UseCase;
using Infrastructure.Adapters;
using UnityEngine;

namespace Bootstrap
{
    public class GameInstaller : MonoBehaviour
    {
        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            var storeRepository = new UnityEconomyStoreRepository();
            ServiceLocator.Instance.RegisterService<IListStoreItemsUseCase>(new ListStoreItemsUseCase(storeRepository));
            ServiceLocator.Instance.RegisterService<IBuyItemUseCase>(new BuyItemUseCase(storeRepository,
                storeRepository));
        }
    }
}