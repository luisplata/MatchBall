using System;

namespace Domain.Model
{
    // Un powerup definido en el dominio
    public class PowerUp
    {
        public string Id { get; }
        public string Name { get; }
        public string Type { get; }
        public int Quantity { get; private set; }

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