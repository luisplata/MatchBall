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

    public event Action<StoreItemData> OnPurchaseSuccess;
    public event Action<StoreItemData, string> OnPurchaseFailed;

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

    public async UniTaskVoid BuyItem(StoreItemData storeItemData)
    {
        try
        {
            // find the item id
            var inventory = EconomyService.Instance.Configuration.GetVirtualPurchases();
            var itemPurchase = inventory.FirstOrDefault(inventoryItem => inventoryItem.Id == storeItemData.purchaseId);
            var item = itemPurchase.Rewards[0].Item.GetReferencedConfigurationItem().CustomDataDeserializable
                .GetAs<CustomPurchaseDataItem>();

            if (!storeItemData.canPurchase)
            {
                throw new Exception("Item cannot be purchased at this time.");
            }

            if (storeItemData.itemCost > await ServiceLocator.Instance.GetService<IPlayerSessionService>()
                    .GetPlayerSessionData().GetCoins())
            {
                throw new Exception("Not enough coins to purchase this item.");
            }

            //Check if power up has player
            if (item.type == "powerup" &&
                ServiceLocator.Instance.GetService<IPlayerSessionService>().CheckPowerUp(item.count))
            {
                throw new Exception($"You already have {item.count} power-up.");
            }

            var purchaseResult =
                await EconomyService.Instance.Purchases.MakeVirtualPurchaseAsync(storeItemData.purchaseId);
            OnPurchaseSuccess?.Invoke(storeItemData);
            switch (item.type)
            {
                case "powerup":
                    ServiceLocator.Instance.GetService<IPlayerSessionService>().AddPowerUpToPlayer(item.count);
                    ServiceLocator.Instance.GetService<IMessageService>()
                        .ShowMessage($"buy {storeItemData.itemName}", 5);
                    break;
            }
        }
        catch (EconomyException ex)
        {
            OnPurchaseFailed?.Invoke(storeItemData, ex.Message);
            ServiceLocator.Instance.GetService<IMessageService>()
                .ErrorMessage($"Failt buy {storeItemData.itemName} cause: {ex.Message}", 5);
        }
        catch (Exception ex)
        {
            OnPurchaseFailed?.Invoke(storeItemData, ex.Message);
            ServiceLocator.Instance.GetService<IMessageService>()
                .ErrorMessage($"Failt buy {storeItemData.itemName} cause: {ex.Message}", 5);
        }
        finally
        {
            ServiceLocator.Instance.GetService<IProfileInfo>().UpdateInfo();
        }
    }

    public async UniTask<List<StoreItemData>> GetStoreItemsData()
    {
        await EconomyService.Instance.Configuration.SyncConfigurationAsync();

        var inventory = EconomyService.Instance.Configuration.GetVirtualPurchases();

        return inventory.Select(inventoryItem =>
        {
            var itemaaa = inventoryItem.CustomDataDeserializable.GetAs<CustomPurchaseData>();
            var items = new StoreItemData
            {
                itemImage = imagesFactory.GetSprite(itemaaa.itemImage),
                itemCost = inventoryItem.Costs[0].Amount,
                itemName = itemaaa.itemName,
                canPurchase = itemaaa.canPurchase,
                purchaseId = inventoryItem.Id
            };
            return items;
        }).ToList();
    }
}


public interface IStoreService
{
    UniTask<List<StoreItemData>> GetStoreItemsData();
    UniTaskVoid BuyItem(StoreItemData storeItemData);
}

public class StoreItemData
{
    public Sprite itemImage;
    public int itemCost;
    public string itemName;
    public bool canPurchase;
    public string purchaseId;
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