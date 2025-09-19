using System.Collections.Generic;
using App.UseCase.GameConfiguration;
using App.UseCase.PowerUps;
using DG.Tweening;
using UnityEngine;

public class ShowPowerUpsInLobby : MonoBehaviour
{
    [SerializeField] private CanvasGroup powerUpsCanvasGroup;
    [SerializeField] private PowerUpsConfiguration powerUpsConfiguration;
    [SerializeField] private PowerUpSelector powerUpSelectorPrefab;
    [SerializeField] private GameObject powerUpsContainer;
    PowerUpsConfiguration _powerUpsConfigurationInstance;
    PlayerSessionDataCustom _playerSessionDataCustom;
    private List<PowerUpSelector> _powerUpSelectors = new();

    private void Start()
    {
        Configure();
        ShowPowerUps();
        HidePowerUps();
    }

    public void Configure()
    {
        powerUpsCanvasGroup.alpha = 0;
        _powerUpsConfigurationInstance = Instantiate(powerUpsConfiguration);
        _playerSessionDataCustom = ServiceLocator.Instance.GetService<IPlayerSessionService>().GetPlayerSessionData();
        foreach (var powerUpSelector in _powerUpSelectors)
        {
            Destroy(powerUpSelector.gameObject);
        }

        _powerUpSelectors.Clear();

        var powerUps = ServiceLocator.Instance.GetService<IPowerUpsUseCase>().GetPowerUps();

        foreach (var powerUpData in powerUps)
        {
            var powerUpSelector = Instantiate(powerUpSelectorPrefab, powerUpsContainer.transform);
            Debug.Log(powerUpData.id);
            var powerUp = _powerUpsConfigurationInstance.GetPowerUpPrefabById(powerUpData.id);
            powerUpSelector.Configuration(powerUp.Art, powerUp.Id, powerUpData.selected, powerUpData.quantity);
            _powerUpSelectors.Add(powerUpSelector);
        }
    }

    public void ShowPowerUps()
    {
        powerUpsCanvasGroup.DOFade(1, 0.5f);
    }

    public void HidePowerUps()
    {
        powerUpsCanvasGroup.DOFade(0, 0.5f);
    }
}