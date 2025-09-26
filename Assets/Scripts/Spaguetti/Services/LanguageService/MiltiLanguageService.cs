using UnityEngine;
using UnityEngine.Localization.Settings;

public class MiltiLanguageService : MonoBehaviour, IMultiLanguageService
{
    private void Awake()
    {
        if (FindObjectsOfType<MiltiLanguageService>().Length > 1)
        {
            Destroy(gameObject);
            return;
        }

        if (ServiceLocator.Instance.RegisterService<IMultiLanguageService>(this))
        {
            DontDestroyOnLoad(gameObject);
        }
    }

    public void ChangeLocale(string localeCode)
    {
        var selectedLocale =
            LocalizationSettings.AvailableLocales.Locales.Find(locale => locale.Identifier.Code == localeCode);
        if (selectedLocale != null)
        {
            LocalizationSettings.SelectedLocale = selectedLocale;
        }
        else
        {
            Debug.LogError("Locale not found: " + localeCode);
        }
    }

    public void ChangeLanguage(string typeOfLanguage)
    {
        ChangeLocale(typeOfLanguage);
    }

    public string GetLanguage()
    {
        return LocalizationSettings.SelectedLocale.Identifier.Code;
    }
}

public interface IMultiLanguageService
{
    void ChangeLanguage(string typeOfLanguage);
    string GetLanguage();
}