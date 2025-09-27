using System.Collections.Generic;
using System.Linq;
using Domain.Model;
using Presentation.Game;

namespace Presentation.ViewModels
{
    public static class StoreItemMapper
    {
        public static ItemStoreDto ToDto(PurchaseOption option, IImageFactory imageFactory,
            IGlobalSessionService globalService)
        {
            // sprite desde una fábrica (clave = ImageKey)
            var sprite = imageFactory.GetSprite(option.ImageKey);

            // costo → string amigable (si hay varias monedas, las unes)
            string cost = string.Join(" + ", option.Costs.Select(c => $"{c.amount} {c.currencyId}"));

            // cantidad → según reglas de negocio (ej. "joker" → número desde servicio global)
            string howMuchPerBuy = "";
            if (option.Reward.type == "powerup")
            {
                int cantidad = globalService.HowMuchPowerUpsByBuy(option.Reward.rewardName);
                howMuchPerBuy = $"X {cantidad}";
            }

            return new ItemStoreDto
            {
                itemImage = sprite,
                itemCost = cost,
                howMuchPerBuy = howMuchPerBuy,
                PurchaseId = option.PurchaseId,
                PurchaseName = option.DisplayName
            };
        }

        public static List<ItemStoreDto> ToDtoList(List<PurchaseOption> options, IImageFactory imageFactory,
            IGlobalSessionService globalService)
        {
            return options.Select(o => ToDto(o, imageFactory, globalService)).ToList();
        }
    }
}