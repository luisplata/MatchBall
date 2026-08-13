using App.Ports;
using Cysharp.Threading.Tasks;
using UnityEngine;

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

        public async UniTask AddPlayer(PlayerSessionDataCustom playerSessionData, string devPlayerKeyChangeInProduction)
        {
            if (string.IsNullOrWhiteSpace(playerSessionData.playerId))
                throw new System.ArgumentException("playerId is required", nameof(playerSessionData.playerId));

            if (string.IsNullOrWhiteSpace(playerSessionData.playerName))
                throw new System.ArgumentException("playerName is required", nameof(playerSessionData.playerName));

            if (string.IsNullOrWhiteSpace(playerSessionData.playerName))
                throw new System.ArgumentException("nickname is required", nameof(playerSessionData.playerName));

            await _webAdapter.AddPlayer(playerSessionData.playerId, playerSessionData.playerName,
                devPlayerKeyChangeInProduction);
            playerSessionData.isCreated = true;
            //serializar el playerSessionData en PlayerPrefs
            PlayerPrefs.SetString("playerSessionData", JsonUtility.ToJson(playerSessionData));
            PlayerPrefs.Save();
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