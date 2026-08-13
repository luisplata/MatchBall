using System;
using System.Collections.Generic;

namespace Domain.Model
{
    [Serializable]
    public class PlayerInventory
    {
        public List<PowerUp> PowerUps;
        public List<Currency> Currencies;

        public PlayerInventory()
        {
            PowerUps = new List<PowerUp>();
            Currencies = new List<Currency>();
        }

        public PlayerInventory(List<PowerUp> powerUps, List<Currency> currencies)
        {
            PowerUps = powerUps;
            Currencies = currencies;
        }
    }
}