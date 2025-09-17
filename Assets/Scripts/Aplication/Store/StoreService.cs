using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Unity.Services.Economy;
using UnityEngine;

public class StoreService : MonoBehaviour, IStoreService
{
    [SerializeField] private Sprite itemImage;
    [SerializeField] private ImagesFactory imagesFactory;

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
        ServiceLocator.Instance.RegisterService<IStoreService>(this);
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


    public async UniTask<List<StoreItemDataFromCloud>> GetStoreItemsData()
    {
        await EconomyService.Instance.Configuration.SyncConfigurationAsync();
        var purchases = EconomyService.Instance.Configuration.GetVirtualPurchases();
        var result = new List<StoreItemDataFromCloud>();

        foreach (var p in purchases)
        {
            var cost = p.Costs.FirstOrDefault();
            var referencedCurrency = cost?.Item?.GetReferencedConfigurationItem();
            var currencyId = referencedCurrency?.Id ?? "coins";
            var price = cost?.Amount ?? 0;

            var reward = p.Rewards.FirstOrDefault();
            var rewardDefinition = reward?.Item?.GetReferencedConfigurationItem();
            var rewardItemId = rewardDefinition?.Id;

            // CustomData de la compra (tienda)
            var purchaseCustom = p.CustomDataDeserializable.GetAs<PurchaseCustomData>();
            // CustomData del reward (qué es lo que entrega)
            var rewardCustom = rewardDefinition?.CustomDataDeserializable?.GetAs<RewardCustomData>();

            var data = new StoreItemDataFromCloud
            {
                PurchaseId = p.Id,
                RewardItemId = rewardItemId,
                Name = purchaseCustom?.itemName ?? p.Name,
                ImageKey = purchaseCustom?.itemImage ?? "",
                CanPurchase = purchaseCustom?.canPurchase ?? true,
                Price = price,
                CurrencyId = currencyId,
                RewardType = rewardCustom?.type ?? "",
                RewardCount = rewardCustom?.count ?? "1"
            };

            // llenar lista de costes si hay varios
            foreach (var c in p.Costs)
            {
                var refCurrency = c.Item?.GetReferencedConfigurationItem();
                data.Costs.Add(new StoreItemCost
                {
                    ResourceId = refCurrency?.Id,
                    Amount = c.Amount
                });
            }

            result.Add(data);
        }

        return result;
    }
}


public interface IStoreService
{
    UniTask<List<StoreItemDataFromCloud>> GetStoreItemsData();
    UniTaskVoid BuyItem(StoreItemDataFromCloud storeItemData);
}

[System.Serializable]
public class PurchaseCustomData
{
    public bool canPurchase;
    public string itemImage;
    public string itemName;
}

[System.Serializable]
public class RewardCustomData
{
    public string count;
    public string type;
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