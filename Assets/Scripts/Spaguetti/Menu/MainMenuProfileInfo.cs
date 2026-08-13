using System;
using App.Ports;
using Cysharp.Threading.Tasks;
using Domain.Events;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuProfileInfo : MonoBehaviour, IWaitToLoadInfo, IProfileInfo
{
    [SerializeField] private Image profileImage;
    [SerializeField] private TextMeshProUGUI profileCups, profileCurrency;

    private void Start()
    {
        UpdateInfo().Forget();
    }

    private void OnEnable()
    {
        ServiceLocator.Instance.RegisterService<IProfileInfo>(this);
        ServiceLocator.Instance.GetService<IEventBus>().Subscribe<CurrencyChangedEvent>(OnCurrencyChanged);
    }

    private void OnCurrencyChanged(CurrencyChangedEvent e)
    {
        profileCurrency.text = e.NewAmount.ToString();
    }

    private void OnDisable()
    {
        ServiceLocator.Instance.UnregisterService<IProfileInfo>();
        ServiceLocator.Instance.GetService<IEventBus>().Unsubscribe<CurrencyChangedEvent>(OnCurrencyChanged);
    }

    public async UniTaskVoid UpdateInfo()
    {
        var playerSession = ServiceLocator.Instance.GetService<IPlayerSessionService>().GetPlayerSessionData();
        var s = await ServiceLocator.Instance.GetService<IInventoryRepository>().GetPlayerInventory();
        //profileImage.sprite = playerSession.playerImage;
        profileCups.text = playerSession.cups.ToString();
        profileCurrency.text = $"{s.Currencies[0].Amount}";
        IsReady = true;
    }

    public bool IsReady { get; private set; }
}

public interface IProfileInfo
{
    UniTaskVoid UpdateInfo();
}

public interface IWaitToLoadInfo
{
    bool IsReady { get; }
}