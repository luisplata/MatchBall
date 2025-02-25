using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuProfileInfo : MonoBehaviour, IWaitToLoadInfo
{
    [SerializeField] private Image profileImage;
    [SerializeField] private TextMeshProUGUI profileCups, profileCurrency;

    private async void Start()
    {
        var playerSession = ServiceLocator.Instance.GetService<IPlayerSessionService>().GetPlayerSessionData();
        //profileImage.sprite = playerSession.playerImage;
        profileCups.text = playerSession.cups.ToString();
        profileCurrency.text = $"{await playerSession.GetCoins()}";
        IsReady = true;
    }

    public bool IsReady { get; private set; }
}

public interface IWaitToLoadInfo
{
    bool IsReady { get; }
}