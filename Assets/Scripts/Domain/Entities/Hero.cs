using System;

namespace Domain.Entities
{
    [Serializable]
    public record Hero(
        int Id,
        string HeroId,
        string Name,
        string Role,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        int currentXp,
        int level,
        int xpToNextLevel,
        int progressPct
    );
}