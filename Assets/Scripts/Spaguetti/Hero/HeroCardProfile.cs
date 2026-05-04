using App.Ports;
using Spaguetti.Hero;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Hero = Domain.Entities.Hero;

public class HeroCardProfile : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI heroName;
    [SerializeField] private Image heroCardImage;
    [SerializeField] private Button startConversation;
    private IHeroController _heroController;
    [SerializeField] private Slider sliderExperience;
    [SerializeField] private TextMeshProUGUI experienceText;
    [SerializeField] private TextMeshProUGUI level;

    public void Configure(Hero hero, IProfilePictureFactory pictureFactory, IHeroController heroController)
    {
        _heroController = heroController;
        heroName.text = hero.Name;
        heroCardImage.sprite = pictureFactory.GetProfilePicture(hero.HeroId);
        startConversation.onClick.AddListener(() => StartConversation(hero, pictureFactory, _heroController));
        level.text = $"{hero.level}";
        SetHeroExperience(hero);
    }

    public void SetHeroExperience(Hero hero)
    {
        if (sliderExperience == null)
            return;

        Debug.Log($"hero {hero}");

        sliderExperience.minValue = 0f;
        sliderExperience.maxValue = 1f;

        sliderExperience.value = hero.progressPct / 100f;
        experienceText.text = $"{hero.progressPct}%";
    }

    private async void StartConversation(Hero hero, IProfilePictureFactory pictureFactory,
        IHeroController heroController)
    {
        var playerId = ServiceLocator.Instance.GetService<IPlayerSessionService>().GetPlayerSessionData().playerId;
        var dialogs = await ServiceLocator.Instance.GetService<IWebAdapter>().StartDialog(hero, playerId);
        ServiceLocator.Instance.GetService<IDialogService>()
            .StartDialog(hero, dialogs, playerId, pictureFactory, heroController);
    }
}