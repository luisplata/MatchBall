using App.Ports;
using Cysharp.Threading.Tasks;

namespace App.UseCase
{
    public class LoginUseCase : ILoginUseCase
    {
        private readonly IWebAdapter _webAdapter;
        private readonly IAdModAdapter _adModAdapter;

        public LoginUseCase(IWebAdapter webAdapter, IAdModAdapter adModAdapter)
        {
            _webAdapter = webAdapter;
            _adModAdapter = adModAdapter;
        }

        public async UniTask LoginPlayer(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
                throw new System.ArgumentException("playerId is required", nameof(playerId));

            await _webAdapter.LoginPlayer(playerId);
            _adModAdapter.Initialize();
        }

        public async UniTask AddPlayer(string playerId, string nickname, string key)
        {
            if (string.IsNullOrWhiteSpace(playerId))
                throw new System.ArgumentException("playerId is required", nameof(playerId));

            if (string.IsNullOrWhiteSpace(nickname))
                throw new System.ArgumentException("nickname is required", nameof(nickname));

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