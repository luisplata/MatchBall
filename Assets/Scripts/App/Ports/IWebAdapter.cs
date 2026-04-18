using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Domain.Entities;

namespace App.Ports
{
    public interface IWebAdapter
    {
        UniTask<PlayerPassBall> GetPlayerPassBall();
        UniTask<List<RewardData>> GetRewards();
        UniTask ClaimReward(int rewardLevel);
        UniTask AddExperience(bool win);
        void UpdateJwtToken(string token);
        UniTask LoginPlayer(string playerId);
        UniTask AddPlayer(string playerId, string nickname, string key);
        UniTask UpdatePlayerNickname(string playerId, string playerName);
        UniTask<List<RewardData>> GetPlayerRewards();
        UniTask<List<Hero>> GetHeroes();
    }
}