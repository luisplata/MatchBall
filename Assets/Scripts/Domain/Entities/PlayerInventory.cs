using System.Collections.Generic;

namespace Domain.Model
{
    public class PlayerInventory
    {
        public List<PowerUp> PowerUps { get; }
        public List<Currency> Currencies { get; }

        public PlayerInventory(List<PowerUp> powerUps, List<Currency> currencies)
        {
            PowerUps = powerUps;
            Currencies = currencies;
        }
    }
}