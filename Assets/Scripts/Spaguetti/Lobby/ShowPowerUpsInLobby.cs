using System.Collections.Generic;
using App.UseCase.GameConfiguration;
using App.UseCase.PowerUps;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

public class ShowPowerUpsInLobby : MonoBehaviour
{
    [SerializeField] private CanvasGroup powerUpsCanvasGroup;
    [SerializeField] private PowerUpsConfiguration powerUpsConfiguration;
    [SerializeField] private PowerUpSelector powerUpSelectorPrefab;
    [SerializeField] private GameObject powerUpsContainer;
    PowerUpsConfiguration _powerUpsConfigurationInstance;
    private List<PowerUpSelector> _powerUpSelectors = new();

    public async UniTask Configure()
    {
        powerUpsCanvasGroup.alpha = 0;

        // One clone per component instead of one per open: the configuration is only read
        // (GetPowerUpPrefabById), so a single instance can be reused forever and re-opening the
        // panel no longer leaks a ScriptableObject per call.
        if (_powerUpsConfigurationInstance == null)
        {
            _powerUpsConfigurationInstance = Instantiate(powerUpsConfiguration);
        }

        foreach (var powerUpSelector in _powerUpSelectors)
        {
            Destroy(powerUpSelector.gameObject);
        }

        _powerUpSelectors.Clear();
        await ServiceLocator.Instance.GetService<IPowerUpsUseCase>().LoadPowerUps();
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