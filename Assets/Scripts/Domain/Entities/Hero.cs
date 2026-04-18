using System;

namespace Domain.Entities
{
    public record Hero(
        int Id,
        string HeroId,
        string Name,
        string Role,
        DateTime CreatedAt,
        DateTime UpdatedAt
    );
}


