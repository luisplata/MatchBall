using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StoreItem : MonoBehaviour
{
    [SerializeField] private Image itemImage;
    [SerializeField] private TextMeshProUGUI itemCost, howMuchPerBuy;
    [SerializeField] private Button buyButton;

    public void Configure(IStoreMediator mediator, StoreItemDataFromCloud storeItemData)
    {
        
    }
}