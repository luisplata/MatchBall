using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Domain.Entities;

namespace App.UseCase.PassBall
{
    public interface IPassBallUseCase
    {
        UniTask<PlayerPassBall> GetPlayerBattlePass();
        UniTask<List<RewardData>> GetPlayerRewards();
    }
}