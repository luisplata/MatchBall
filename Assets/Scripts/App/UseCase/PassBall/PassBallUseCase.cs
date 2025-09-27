using System.Collections.Generic;
using App.Ports;
using Cysharp.Threading.Tasks;
using Domain.Entities;

namespace App.UseCase.PassBall
{
    public class PassBallUseCase : IPassBallUseCase
    {
        private readonly IWebAdapter _webAdapter;

        public PassBallUseCase(IWebAdapter webAdapter)
        {
            _webAdapter = webAdapter;
        }

        public async UniTask<PlayerPassBall> GetPlayerBattlePass()
        {
            return await _webAdapter.GetPlayerPassBall();
        }

        public async UniTask<List<RewardData>> GetPlayerRewards()
        {
            return await _webAdapter.GetPlayerRewards();
        }
    }
}