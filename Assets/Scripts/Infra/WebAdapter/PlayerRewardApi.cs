using System;
using System.Collections.Generic;
using System.Net.Http;
using Cysharp.Threading.Tasks;
using Domain.Entities;
using Infra.WebAdapter.Mapper;
using UnityEngine;

namespace Infra.WebAdapter
{
    public class PlayerRewardApi : ApiBase
    {
        public PlayerRewardApi(string serverUrl, string uri, IHttpMediator mediator)
            : base(serverUrl, uri, mediator)
        {
        }

        public async UniTask<List<RewardData>> GetRewards()
        {
            var requestUri = $"{ServerUrl}{Uri}/battle-pass-reward";
            var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", JwtToken);

            var response = await Client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return new List<RewardData>();

            var responseData = await response.Content.ReadAsStringAsync();
            Debug.Log($"Rewards response: {responseData}");

            // Como es un array, usamos wrapper
            var listWrapper = new RewardDtoListWrapper { rewards = JsonHelper.FromJson<RewardDto>(responseData) };
            return listWrapper.rewards.ConvertAll(PlayerRewardMapper.ToDomain);
        }

        public async UniTask<PlayerPassBall> GetPlayerPassBall()
        {
            var id = ServiceLocator.Instance.GetService<IPlayerSessionService>()
                .GetPlayerSessionData().playerId;
            var requestUri = $"{ServerUrl}{Uri}/battle-pass/{id}";

            var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", JwtToken);

            var response = await Client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                throw new Exception($"Error getting player pass ball {response.StatusCode}");

            var responseData = await response.Content.ReadAsStringAsync();
            Debug.Log($"PassBall response: {responseData}");

            var wrapper = JsonUtility.FromJson<PlayerPassBallResponseDto>(responseData);

            // Mapear usando tu PlayerRewardMapper
            return PlayerRewardMapper.ToDomain(wrapper.data.battlePass, wrapper.data.rewards);
        }

        public async UniTask<ClaimRewardResponse> ClaimReward(int rewardDataLevel)
        {
            var id = ServiceLocator.Instance
                .GetService<IPlayerSessionService>()
                .GetPlayerSessionData().playerId;

            var requestUri = $"{ServerUrl}{Uri}/player-reward/claim";

            var json = JsonUtility.ToJson(new ClaimRewardRequest
            {
                playerId = id,
                level = rewardDataLevel
            });

            var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", JwtToken);

            var response = await Client.SendAsync(request);
            var responseData = await response.Content.ReadAsStringAsync();
            Debug.Log($"ClaimReward response: {responseData}");

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Error claiming reward: {response.StatusCode}");

            return JsonUtility.FromJson<ClaimRewardResponse>(responseData);
        }

        public async UniTask<PlayerPassBall> AddExperience(bool win)
        {
            var experienceToWin = win
                ? ServiceLocator.Instance.GetService<IGlobalSessionService>().GetExperienceToWin()
                : ServiceLocator.Instance.GetService<IGlobalSessionService>().GetExperienceToLose();

            var id = ServiceLocator.Instance.GetService<IPlayerSessionService>()
                .GetPlayerSessionData().playerId;
            var requestUri = $"{ServerUrl}{Uri}/battle-pass/experience";

            var json = JsonUtility.ToJson(new ExperienceRequest { experience = experienceToWin, playerId = id });
            var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", JwtToken);

            var response = await Client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                throw new Exception("Error adding experience");

            var responseData = await response.Content.ReadAsStringAsync();
            Debug.Log($"AddExperience response: {responseData}");

            var wrapper = JsonUtility.FromJson<AddExperienceResponseDto>(responseData);
            var dto = wrapper.data;

            // si hay reward, lo puedes mapear también
            RewardData rewardData = null;
            if (dto.reward != null)
            {
                // RewardEarnedDto -> RewardData
                string rewardJson = JsonUtility.ToJson(dto.reward);
                rewardData = new RewardData(
                    dto.reward.rewardLevel,
                    dto.reward.rewardLevel,
                    rewardJson
                );
            }

            return new PlayerPassBall(
                dto.id,
                dto.playerId,
                dto.level,
                dto.experience,
                DateTime.Parse(dto.created_at, null, System.Globalization.DateTimeStyles.RoundtripKind),
                DateTime.Parse(dto.updated_at, null, System.Globalization.DateTimeStyles.RoundtripKind),
                new List<PlayerReward>() // por ahora vacío; rewards se piden con otro endpoint
            );
        }
    }

    [Serializable]
    public class RewardDtoListWrapper
    {
        public List<RewardDto> rewards;
    }

    [Serializable]
    public class PlayerPassBallResponseDto
    {
        public bool success;
        public PlayerPassBallDataDto data;
    }

    [Serializable]
    public class PlayerPassBallDataDto
    {
        public PlayerPassBallDto battlePass;
        public List<PlayerRewardDto> rewards;
    }

    [Serializable]
    public class PlayerPassBallDto
    {
        public int id;
        public string playerId;
        public int level;
        public int experience;
        public string created_at;
        public string updated_at;
    }

    [Serializable]
    public class PlayerRewardDto
    {
        public int id;
        public string playerId;
        public int rewardId;
        public string awarded_at;
        public int claimed; // 0 o 1
        public int level;   // <-- agregado
        public RewardDetail reward; // <-- agregado
    }

    [Serializable]
    public class ClaimRewardRequest
    {
        public string playerId;
        public int level;
    }

    [Serializable]
    public class ClaimRewardResponse
    {
        public string message;
    }


    [Serializable]
    public class ExperienceRequest
    {
        public int experience;
        public string playerId;
    }


    [Serializable]
    public class AddExperienceResponseDto
    {
        public bool success;
        public AddExperienceDataDto data;
    }

    [Serializable]
    public class AddExperienceDataDto
    {
        public int id;
        public string playerId;
        public int level;
        public int experience;
        public string created_at;
        public string updated_at;
        public RewardEarnedDto reward; // puede ser null si no hay levelUp
        public bool levelUp;
    }

    [Serializable]
    public class RewardEarnedDto
    {
        public string type;
        public string body;
        public bool isMaxLevelReward;
        public int rewardLevel;
    }

    [Serializable]
    public class RewardDto
    {
        public int id;
        public int level;
        public RewardDetail reward; // <-- aquí string, no RewardDetail
        public string created_at;
        public string updated_at;
    }

    [Serializable]
    public class RewardDetail
    {
        public string type;
        public string body; // puede ser dinámico, luego lo parseas a lo que necesites
    }
}