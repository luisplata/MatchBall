using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StoreItem : MonoBehaviour
{
    [SerializeField] private Image itemImage;
    [SerializeField] private TextMeshProUGUI itemCost, howMuchPerBuy;
    [SerializeField] private Button buyButton;

    public void Configure(IStoreMediator mediator, StoreItemData storeItemData)
    {
        itemImage.sprite = storeItemData.itemImage;
        itemCost.text = storeItemData.itemCost.ToString();
        buyButton.onClick.AddListener(() => { mediator.BuyItem(storeItemData); });
        var powerup = storeItemData.itemName.Split("Power Up ")[1];
        howMuchPerBuy.text =
            $"X {ServiceLocator.Instance.GetService<IGlobalSessionService>().howMuchPowerUpsByBuy(powerup)}";
    }
}