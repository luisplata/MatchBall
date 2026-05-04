using System;
using System.Collections.Generic;
using App.Ports;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Spaguetti.Hero
{
    public class HeroController : MonoBehaviour, IProfilePictureFactory, IHeroController
    {
        [SerializeField] private HeroCardProfile heroCardPrefab;
        [SerializeField] private Transform cardsParent;
        [SerializeField] private Sprite defaultProfilePicture;

        [SerializeField] private List<SpriteImages> spritesImages;
        private Dictionary<string, SpriteImages> _spritesImagesDict = new();

        List<HeroCardProfile> heroCards = new();

        public async UniTask LoadHeroes()
        {
            foreach (var hero in heroCards)
            {
                Destroy(hero.gameObject);
            }

            heroCards.Clear();

            var heroes = await ServiceLocator.Instance.GetService<IWebAdapter>().GetHeroes(
                ServiceLocator.Instance.GetService<IPlayerSessionService>().GetPlayerSessionData().playerId
            );

            foreach (var hero in heroes)
            {
                var card = Instantiate(heroCardPrefab, cardsParent);
                card.Configure(hero, this, this);
                heroCards.Add(card);
            }
        }

        private async void Start()
        {
            await LoadHeroes();
        }

        public Sprite GetProfilePicture(string heroId)
        {
            if (_spritesImagesDict.TryGetValue(heroId, out var spriteImages))
            {
                return spriteImages.sprite;
            }

            return defaultProfilePicture;
        }
    }
}