using Spaguetti.Hero;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Hero = Domain.Entities.Hero;

public class HeroCardProfile : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI heroName;
    [SerializeField] private Image heroCardImage;

    public void Configure(Hero hero, IProfilePictureFactory pictureFactory)
    {
        heroName.text = hero.Name;
        heroCardImage.sprite = pictureFactory.GetProfilePicture(hero.Id);
    }
}