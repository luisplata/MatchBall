using App.Ports;
using Cysharp.Threading.Tasks;

namespace App.UseCase
{
    public class LoginUseCase : ILoginUseCase
    {
        private readonly IWebAdapter _webAdapter;

        public LoginUseCase(IWebAdapter webAdapter)
        {
            _webAdapter = webAdapter;
        }

        public async UniTask LoginPlayer(string playerId)
        {
            await _webAdapter.LoginPlayer(playerId);
        }

        public async UniTask AddPlayer(string playerId, string nickname, string key)
        {
            await _webAdapter.AddPlayer(playerId, nickname, key);
        }

        public async UniTask UpdatePlayerNickname(string playerId, string playerName)
        {
            await _webAdapter.UpdatePlayerNickname(playerId, playerName);
        }

        public async UniTask ClaimReward(int rewardDataLevel)
        {
            await _webAdapter.ClaimReward(rewardDataLevel);
        }

        public async UniTask AddExperience(bool b)
        {
            await _webAdapter.AddExperience(b);
        }
    }
}