using App.Ports;
using UnityEngine;

namespace Spaguetti.Hero
{
    public class HeroController : MonoBehaviour, IProfilePictureFactory
    {
        [SerializeField] private HeroCardProfile heroCardPrefab;
        [SerializeField] private Transform cardsParent;
        [SerializeField] private Sprite defaultProfilePicture;

        public async void Start()
        {
            var heroes = await ServiceLocator.Instance.GetService<IWebAdapter>().GetHeroes(
                ServiceLocator.Instance.GetService<IPlayerSessionService>().GetPlayerSessionData().playerId
            );

            foreach (var hero in heroes)
            {
                Instantiate(heroCardPrefab, cardsParent).Configure(hero, this);
            }
        }

        public Sprite GetProfilePicture(int heroId)
        {
            return defaultProfilePicture;
        }
    }
}