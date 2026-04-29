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

    public void Configure(Hero hero, IProfilePictureFactory pictureFactory)
    {
        heroName.text = hero.Name;
        heroCardImage.sprite = pictureFactory.GetProfilePicture(hero.Id);
        startConversation.onClick.AddListener(() => StartConversation(hero));
    }

    private void StartConversation(Hero hero)
    {
         ServiceLocator.Instance.GetService<IDialogService>().StartDialog(hero);
         
    }
}