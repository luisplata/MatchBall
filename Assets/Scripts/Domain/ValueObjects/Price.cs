namespace Domain.ValueObjects
{
    public class Price
    {
        public string CurrencyId { get; }
        public int Amount { get; }

        public Price(string currencyId, int amount)
        {
            CurrencyId = currencyId;
            Amount = amount;
        }
    }
}