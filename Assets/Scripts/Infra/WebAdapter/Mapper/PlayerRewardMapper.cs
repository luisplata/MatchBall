using System;
using System.Collections.Generic;
using UnityEngine;

namespace Infra.WebAdapter.Mapper
{
    using System.Linq;
    using Domain.Entities;

    public static class PlayerRewardMapper
    {
        public static PlayerPassBall ToDomain(PlayerPassBallDto dto, List<PlayerRewardDto> rewards)
        {
            return new PlayerPassBall(
                dto.id,
                dto.playerId,
                dto.level,
                dto.experience,
                DateTime.Parse(dto.created_at, null, System.Globalization.DateTimeStyles.RoundtripKind),
                DateTime.Parse(dto.updated_at, null, System.Globalization.DateTimeStyles.RoundtripKind),
                rewards?.Select(r => new PlayerReward(
                    r.id,
                    r.playerId,
                    r.rewardId,
                    string.IsNullOrEmpty(r.awarded_at)
                        ? DateTime.MinValue
                        : DateTime.Parse(r.awarded_at, null, System.Globalization.DateTimeStyles.RoundtripKind),
                    r.claimed == 1,
                    r.level,
                    r.reward?.type,
                    r.reward?.body
                )).ToList()
            );
        }

        public static RewardData ToDomain(RewardDto dto)
        {
            string rewardJson = JsonUtility.ToJson(dto.reward);
            return new RewardData(
                dto.id,
                dto.level,
                rewardJson
            );
        }
    }
}