using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using App.Ports;
using Domain.Entities;

namespace Infra.WebAdapter
{
    public class WebAdapter : IWebAdapter, IHttpMediator
    {
        private readonly PlayerApi _playerApi;
        private readonly BattlePassApi _battlePassApi;
        private readonly PlayerRewardApi _playerRewardApi;

        public WebAdapter(string serverUrl, string uri)
        {
            _playerApi = new PlayerApi(serverUrl, uri, this);
            _battlePassApi = new BattlePassApi(serverUrl, uri, this);
            _playerRewardApi = new PlayerRewardApi(serverUrl, uri, this);
        }

        public async UniTask<PlayerPassBall> GetPlayerPassBall()
        {
            return await SafeExecute(
                () => _playerRewardApi.GetPlayerPassBall(),
                new PlayerPassBall(0, string.Empty, 0, 0, DateTime.MinValue, DateTime.MinValue,
                    new List<PlayerReward>()),
                nameof(GetPlayerPassBall)
            );
        }

        public async UniTask<List<RewardData>> GetRewards()
        {
            return await SafeExecute(
                () => _playerRewardApi.GetRewards(),
                new List<RewardData>(),
                nameof(GetRewards)
            );
        }

        public async UniTask ClaimReward(int rewardLevel)
        {
            await SafeExecute(
                () => _playerRewardApi.ClaimReward(rewardLevel),
                nameof(ClaimReward)
            );
        }

        public async UniTask AddExperience(bool win)
        {
            await SafeExecute(
                () => _playerRewardApi.AddExperience(win),
                nameof(AddExperience)
            );
        }

        public void UpdateJwtToken(string token)
        {
            _playerApi.JwtToken = token;
            _battlePassApi.JwtToken = token;
            _playerRewardApi.JwtToken = token;
        }

        public async UniTask LoginPlayer(string playerId)
        {
            await _playerApi.LoginPlayer(playerId);
        }

        public async UniTask AddPlayer(string playerId, string nickname, string key)
        {
            //TODO: Return Player Model
            await _playerApi.AddPlayer(playerId, nickname, key);
        }

        public async UniTask UpdatePlayerNickname(string playerId, string playerName)
        {
            await _playerApi.UpdatePlayerNickname(playerId, playerName);
        }

        public async UniTask<List<RewardData>> GetPlayerRewards()
        {
            return await _playerRewardApi.GetRewards();
        }

        // 🔹 Método helper para operaciones que devuelven algo
        private async UniTask<T> SafeExecute<T>(Func<UniTask<T>> action, T fallback, string context)
        {
            try
            {
                return await action();
            }
            catch (Exception ex)
            {
                HandleError(context, ex);
                return fallback;
            }
        }

        // 🔹 Método helper para operaciones void/Task
        private async UniTask SafeExecute(Func<UniTask> action, string context)
        {
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                HandleError(context, ex);
                throw;
            }
        }

        private void HandleError(string context, Exception ex)
        {
            UnityEngine.Debug.LogError($"[{nameof(WebAdapter)}] Error in {context}: {ex.Message}");
            ServiceLocator.Instance
                .GetService<IMessageService>()
                .ErrorMessage($"Error in {context}: {ex.Message}", 3).Forget();
        }
    }
}