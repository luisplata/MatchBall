using System;

namespace Domain.Model
{
    [Serializable]
    public class Currency
    {
        public string Id;
        public int Amount;

        public Currency(string id, int amount)
        {
            Id = id;
            Amount = amount;
        }

        public void Spend(int cost)
        {
            if (Amount < cost)
                throw new InvalidOperationException("Not enough currency.");
            Amount -= cost;
        }

        public void Add(int amount) => Amount += amount;
    }
}