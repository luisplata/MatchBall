using System;

namespace Domain.Model
{
    public class Currency
    {
        public string Id { get; }
        public int Amount { get; private set; }

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