using UnityEngine;
using UnityEngine.UI;

public class ConfigLanguage : MonoBehaviour
{
    private IConfigLanguageMediator _mediator;
    [SerializeField] private Button esButton, enButton, itButton, frButton;
    [SerializeField] private Button closeButton;
    private PlayerSessionDataCustom _player;
    private string _language;

    public void Configure(IConfigLanguageMediator mediator)
    {
        _mediator = mediator;

        esButton.onClick.AddListener(ChangeLanguageToEs);
        enButton.onClick.AddListener(ChangeLanguageToEn);
        itButton.onClick.AddListener(ChangeLanguageToIt);
        frButton.onClick.AddListener(ChangeLanguageToFr);
        closeButton.onClick.AddListener(SaveData);
        Show();
    }


    private void Show()
    {
        _player = ServiceLocator.Instance.GetService<IPlayerSessionService>().GetPlayerSessionData();
        _language = _player.language;
        switch (_player.language)
        {
            case "es":
                esButton.Select();
                break;
            case "en":
                enButton.Select();
                break;
            case "it":
                itButton.Select();
                break;
            case "fr":
                frButton.Select();
                break;
            default:
                esButton.Select();
                break;
        }
    }

    public void SaveData()
    {
        _player.language = _language;
        ServiceLocator.Instance.GetService<IStorageData>().UpdatePlayerData(_player);
    }

    public void ChangeLanguageToEs()
    {
        _language = "es";
        ServiceLocator.Instance.GetService<IMultiLanguageService>().ChangeLanguage(_language);
    }

    public void ChangeLanguageToEn()
    {
        _language = "en";
        ServiceLocator.Instance.GetService<IMultiLanguageService>().ChangeLanguage(_language);
    }

    public void ChangeLanguageToIt()
    {
        _language = "it";
        ServiceLocator.Instance.GetService<IMultiLanguageService>().ChangeLanguage(_language);
    }

    public void ChangeLanguageToFr()
    {
        _language = "fr";
        ServiceLocator.Instance.GetService<IMultiLanguageService>().ChangeLanguage(_language);
    }
}