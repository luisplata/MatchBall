namespace Infra.WebAdapter.Mapper
{
    using System.Linq;
    using Domain.Entities;

    public static class PlayerRewardMapper
    {
        public static PlayerPassBall ToDomain(PlayerPassBallDto dto)
        {
            return new PlayerPassBall(
                dto.id,
                dto.playerId,
                dto.level,
                dto.experience,
                dto.created_at,
                dto.updated_at,
                dto.playerRewards?
                    .Select(r => new PlayerReward(
                        r.id,
                        r.playerId,
                        r.rewardId,
                        r.awarded_at,
                        r.claimed == 1
                    ))
                    .ToList()
            );
        }

        public static RewardData ToDomain(RewardDto dto)
        {
            // guardamos reward como raw JSON (string)
            string rewardJson = UnityEngine.JsonUtility.ToJson(dto.reward);
            return new RewardData(
                dto.id,
                dto.level,
                rewardJson
            );
        }
    }
}