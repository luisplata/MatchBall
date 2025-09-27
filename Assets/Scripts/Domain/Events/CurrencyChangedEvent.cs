namespace Domain.Events
{
    public record CurrencyChangedEvent(string CurrencyId, int NewAmount);
}