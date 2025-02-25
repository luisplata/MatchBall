using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Economy;
using Unity.Services.Economy.Model;
using UnityEngine;

public class StoreService : MonoBehaviour, IStoreService
{
    [SerializeField] private Sprite itemImage;

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

    public void BuyItem(StoreItemData storeItemData)
    {
    }

    public async UniTask<List<StoreItemData>> GetStoreItemsData()
    {
        await EconomyService.Instance.Configuration.SyncConfigurationAsync();
        var inventory = EconomyService.Instance.Configuration.GetVirtualPurchases();

        var dummyData = new List<StoreItemData>
        {
            new()
            {
                itemImage = itemImage,
                itemCost = 100,
                itemName = "Item 1"
            },
            new()
            {
                itemImage = itemImage,
                itemCost = 200,
                itemName = "Item 2"
            },
            new()
            {
                itemImage = itemImage,
                itemCost = 300,
                itemName = "Item 3"
            },
            new()
            {
                itemImage = itemImage,
                itemCost = 400,
                itemName = "Item 4"
            },
            new()
            {
                itemImage = itemImage,
                itemCost = 500,
                itemName = "Item 5"
            }
        };

        if (inventory.Count > 0)
        {
            return inventory.Select(inventoryItem => new StoreItemData
            {
                itemImage = itemImage,
                itemCost = inventoryItem.Rewards[0].Amount,
                itemName = inventoryItem.Rewards[0].Item.GetReferencedConfigurationItem().Name
            }).Concat(dummyData).ToList();
        }
        else
        {
            return dummyData; 
        }
    }
}


public interface IStoreService
{
    UniTask<List<StoreItemData>> GetStoreItemsData();
}