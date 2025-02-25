using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StoreItem : MonoBehaviour
{
    [SerializeField] private Image itemImage;
    [SerializeField] private TextMeshProUGUI itemCost;
    [SerializeField] private Button buyButton;
    
    public void Configure(IStoreMediator mediator, StoreItemData storeItemData)
    {
        itemImage.sprite = storeItemData.itemImage;
        itemCost.text = storeItemData.itemCost.ToString();
        buyButton.onClick.AddListener(() =>
        {
            mediator.BuyItem(storeItemData);
        });
    }
}

public class StoreItemData
{
    public Sprite itemImage;
    public int itemCost;
    public string itemName;
}