using System;
using System.Collections.Generic;

namespace Domain.Entities
{
    public record PlayerPassBall(
        int Id,
        string PlayerId,
        int Level,
        int Experience,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        IReadOnlyList<PlayerReward> PlayerRewards
    );
}