using System;
using System.Collections.Generic;
using System.Net.Http;
using Cysharp.Threading.Tasks;
using Domain.Entities;
using Infra.WebAdapter.Mapper;
using Newtonsoft.Json.Linq;
using UnityEngine;

[Serializable]
public class ClaimRewardRequest
{
    public string playerId;
    public int level;
}

[Serializable]
public class ExperienceRequest
{
    public int experience;
    public string playerId;
}

[Serializable]
public class PlayerPassBallDto
{
    public int id;
    public string playerId;
    public int level;
    public int experience;
    public DateTime created_at;
    public DateTime updated_at;
    public List<PlayerRewardDto> playerRewards;
}

[Serializable]
public class PlayerRewardDto
{
    public int id;
    public string playerId;
    public int rewardId;
    public DateTime awarded_at;
    public int claimed;
}

[Serializable]
public class RewardDto
{
    public int id;
    public int level;
    public RewardDetail reward;
}

[Serializable]
public class RewardDetail
{
    public string type;
    public JObject body; // puede ser dinámico, luego lo parseas a lo que necesites
}

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
        var id = ServiceLocator.Instance.GetService<IPlayerSessionService>().GetPlayerSessionData().playerId;
        var requestUri = $"{ServerUrl}{Uri}/battle-pass/{id}";

        var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", JwtToken);

        var response = await Client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
            throw new Exception($"Error getting player pass ball {response.IsSuccessStatusCode}");

        var responseData = await response.Content.ReadAsStringAsync();
        Debug.Log($"PassBall response: {responseData}");

        var dto = JsonUtility.FromJson<PlayerPassBallDto>(responseData);
        return PlayerRewardMapper.ToDomain(dto);
    }

    public async UniTask ClaimReward(int rewardDataLevel)
    {
        var id = ServiceLocator.Instance.GetService<IPlayerSessionService>().GetPlayerSessionData().playerId;
        var requestUri = $"{ServerUrl}{Uri}/player-reward/claim";

        var json = JsonUtility.ToJson(new ClaimRewardRequest { playerId = id, level = rewardDataLevel });
        var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", JwtToken);

        var response = await Client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
            throw new Exception("Error claiming reward");
    }

    public async UniTask<PlayerPassBall> AddExperience(bool win)
    {
        var experienceToWin = win
            ? ServiceLocator.Instance.GetService<IGlobalSessionService>().GetExperienceToWin()
            : ServiceLocator.Instance.GetService<IGlobalSessionService>().GetExperienceToLose();

        var id = ServiceLocator.Instance.GetService<IPlayerSessionService>().GetPlayerSessionData().playerId;
        var requestUri = $"{ServerUrl}{Uri}/battle-pass/experience";

        var json = JsonUtility.ToJson(new ExperienceRequest { experience = experienceToWin, playerId = id });
        var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", JwtToken);

        var response = await Client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
            throw new Exception("Error adding experience");

        var responseData = await response.Content.ReadAsStringAsync();
        var dto = JsonUtility.FromJson<PlayerPassBallDto>(responseData);
        return PlayerRewardMapper.ToDomain(dto);
    }
}

[Serializable]
public class RewardDtoListWrapper
{
    public List<RewardDto> rewards;
}