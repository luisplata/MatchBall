using System;

namespace Domain.Entities
{
    public record PlayerReward(
        int Id,
        string PlayerId,
        int RewardId,
        DateTime AwardedAt,
        bool Claimed
    );
}