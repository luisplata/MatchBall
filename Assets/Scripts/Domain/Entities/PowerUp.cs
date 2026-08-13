using System;

namespace Domain.Model
{
    // Un powerup definido en el dominio
    [Serializable]
    public class PowerUp
    {
        public string Id;
        public string Name;
        public string Type;
        public int Quantity;

        public PowerUp(string id, string name, string type, int quantity)
        {
            Id = id;
            Name = name;
            Type = type;
            Quantity = quantity;
        }

        public void Consume(int amount = 1)
        {
            if (Quantity < amount)
                throw new InvalidOperationException("Not enough powerups.");
            Quantity -= amount;
        }

        public void Add(int amount) => Quantity += amount;
    }
}