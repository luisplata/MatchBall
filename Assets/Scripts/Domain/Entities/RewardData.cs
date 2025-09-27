namespace Domain.Entities
{
    public record RewardData(
        int Id,
        int Level,
        string Reward // raw JSON string, lo parseas luego en un UseCase
    );
}