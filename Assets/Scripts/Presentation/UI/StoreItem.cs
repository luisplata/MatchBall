using Presentation.Mediators;
using Presentation.ViewModels;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Presentation.UI
{
    public class StoreItem : MonoBehaviour
    {
        [SerializeField] private Image itemImage;
        [SerializeField] private TextMeshProUGUI itemCost, howMuchPerBuy;
        [SerializeField] private Button buyButton;

        public void Configure(IStoreMediator mediator, ItemStoreDto storeItemData)
        {
            itemImage.sprite = storeItemData.itemImage;
            itemCost.text = storeItemData.itemCost;
            howMuchPerBuy.text = storeItemData.howMuchPerBuy;
            //TODO: Buy Item
            buyButton.onClick.AddListener(() => { mediator.BuyItem(storeItemData); });
        }
    }
}