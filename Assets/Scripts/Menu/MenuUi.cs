using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MenuUi : MonoBehaviour, IMenuUi
{
    [SerializeField] private Image profileImage;
    [SerializeField] private TextMeshProUGUI coinsText;
    [SerializeField] private TextMeshProUGUI cupsText;

    private void OnEnable()
    {
        ServiceLocator.Instance.RegisterService<IMenuUi>(this);
    }

    private void OnDisable()
    {
        ServiceLocator.Instance.UnregisterService<IMenuUi>();
    }

    public async UniTask UpdatePlayerData(PlayerSessionDataCustom playerData)
    {
        //profileImage.sprite = playerData.playerPhotoUrl;
        var s = await playerData.GetCoins();
        coinsText.text = $"{s}";
        cupsText.text = playerData.cups.ToString();
    }
}

public interface IMenuUi
{
    UniTask UpdatePlayerData(PlayerSessionDataCustom playerData);
}