using System;
using System.Collections.Generic;
using App.UseCase;
using Cysharp.Threading.Tasks;
using Domain.Model;
using Unity.Services.Economy;
using UnityEngine;

namespace Presentation.Game
{
    public class StoreService : MonoBehaviour
    {
        [SerializeField] private Sprite itemImage;
        [SerializeField] private ImagesFactory imagesFactory;

        private ListStoreItemsUseCase listStoreItemsUseCase;

        public event Action<StoreItemDataFromCloud> OnPurchaseSuccess;
        public event Action<StoreItemDataFromCloud, string> OnPurchaseFailed;

        private void Awake()
        {
            if (FindObjectsOfType<StoreService>().Length > 1)
            {
                Destroy(gameObject);
                return;
            }

            DontDestroyOnLoad(gameObject);
        }

        public async UniTaskVoid BuyItem(StoreItemDataFromCloud storeItemData)
        {
            if (!storeItemData.CanPurchase)
            {
                Debug.LogWarning($"{storeItemData.Name} no se puede comprar");
                return;
            }

            try
            {
                var result = await EconomyService.Instance.Purchases
                    .MakeVirtualPurchaseAsync(storeItemData.PurchaseId);

                // entregar el reward al jugador según configuración
                switch (storeItemData.RewardType)
                {
                    case "powerup":
                        int amount = ServiceLocator.Instance
                            .GetService<IGlobalSessionService>()
                            .howMuchPowerUpsByBuy(storeItemData.RewardCount);

                        ServiceLocator.Instance.GetService<IPlayerSessionService>()
                            .AddPowerUpToPlayer(storeItemData, amount);

                        break;

                    case "coins":
                        int coins = int.Parse(storeItemData.RewardCount);
                        ServiceLocator.Instance.GetService<IPlayerSessionService>()
                            .AddCoins(coins);
                        break;
                }
            }
            catch (EconomyException e)
            {
                Debug.LogError($"Error en compra: {e.Message}");
            }
        }

        public async UniTaskVoid ConsumePowerUp(string rewardItemId, int amount = 1)
        {
        }


        public async UniTask<List<PurchaseOption>> GetStoreItemsData()
        {
            return await listStoreItemsUseCase.Execute();
        }
    }

    [System.Serializable]
    public class PurchaseCustomData
    {
        public bool canPurchase;
        public string itemImage;
        public string itemName;
    }


    public class StoreItemData
    {
        public Sprite itemImage;
        public int itemCost;
        public string itemName;
        public bool canPurchase;
        public string purchaseId;
    }

    [System.Serializable]
    public class StoreItemCost
    {
        public string ResourceId; // moneda (ej. "coins")
        public int Amount; // cantidad (ej. 100)
    }

    [System.Serializable]
    public class StoreItemDataFromCloud
    {
        public string PurchaseId; // id del VirtualPurchase
        public string RewardItemId; // id del InventoryItem
        public string Name; // "Power Up ICE"
        public string ImageKey; // "ICE"
        public int Price; // costo principal
        public string CurrencyId; // "coins"
        public bool CanPurchase; // true/false
        public List<StoreItemCost> Costs = new();

        // datos de reward
        public string RewardType; // "powerup", "coins", etc.
        public string RewardCount; // "joker" (o número si aplica)
    }

    [Serializable]
    public class CustomPurchaseData
    {
        public string itemImage;
        public string itemName;
        public bool canPurchase;
    }

    [Serializable]
    public class CustomPurchaseDataCoint
    {
        public string itemImage;
        public string itemName;
        public bool canPurchase;
    }

    [Serializable]
    public class CustomPurchaseDataItem
    {
        public string count;
        public string type;
    }
}