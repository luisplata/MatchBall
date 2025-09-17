using System.Collections.Generic;
using UnityEngine;

public class StoreMediator : MonoBehaviour, IStoreMediator, IWaitToLoadInfo
{
    [SerializeField] private StoreItem storeItem;
    [SerializeField] private Transform storeItemsParent;
    private List<StoreItemDataFromCloud> _storeItemsData;
    private List<StoreItem> _storeItems = new();

    private async void Start()
    {
        _storeItemsData = await ServiceLocator.Instance.GetService<IStoreService>().GetStoreItemsData();
        //clean all items
        foreach (var item in _storeItems)
        {
            Destroy(item.gameObject);
        }

        _storeItems.Clear();
        foreach (var storeItemData in _storeItemsData)
        {
            var item = Instantiate(storeItem, storeItemsParent);
            item.Configure(this, storeItemData);
            _storeItems.Add(item);
        }

        IsReady = true;
    }

    public void BuyItem(StoreItemDataFromCloud storeItemData)
    {
        ServiceLocator.Instance.GetService<IStoreService>().BuyItem(storeItemData).Forget();
    }

    public bool IsReady { get; private set; }
}

public interface IStoreMediator
{
    void BuyItem(StoreItemDataFromCloud storeItemData);
}