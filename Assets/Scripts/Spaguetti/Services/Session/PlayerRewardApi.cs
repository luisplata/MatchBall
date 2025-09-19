using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class PlayerRewardApi : ApiBase
{
    public PlayerRewardApi(string serverUrl, string uri, IHttpMediator mediator)
        : base(serverUrl, uri, mediator)
    {
    }

    public async UniTask<List<RewardData>> GetRewards()
    {
        var requestUri = ServerUrl + Uri + "/battle-pass-reward";
        Debug.Log("Request URI PlayerRewardApi: " + requestUri);
        var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", JwtToken);
        var response = await Client.SendAsync(request);
        if (response.IsSuccessStatusCode)
        {
            var responseData = await response.Content.ReadAsStringAsync();
            Debug.Log("Response Data PlayerRewardApi: " + responseData);
            return JsonHelper.FromJson<RewardData>(responseData);
        }
        else
        {
            return new List<RewardData>();
        }
    }

    public async UniTask<PlayerBattlePass> GetPlayerBattlePass()
    {
        var id = ServiceLocator.Instance.GetService<IPlayerSessionService>().GetPlayerSessionData().playerId;
        var requestUri = ServerUrl + Uri + $"/battle-pass/{id}";
        Debug.Log("Request URI PlayerRewardApi: " + requestUri);
        var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", JwtToken);
        var response = await Client.SendAsync(request);
        if (response.IsSuccessStatusCode)
        {
            var responseData = await response.Content.ReadAsStringAsync();
            Debug.Log($"Response {responseData}");
            return JsonUtility.FromJson<PlayerBattlePass>(responseData);
        }
        else
        {
            return new PlayerBattlePass();
        }
    }

    public async UniTask ClaimReward(int rewardDataLevel)
    {
        var id = ServiceLocator.Instance.GetService<IPlayerSessionService>().GetPlayerSessionData().playerId;
        var requestUri = ServerUrl + Uri + $"/player-reward/claim";
        var json = JsonUtility.ToJson(new ClaimRewardRequest { playerId = id, level = rewardDataLevel });
        var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", JwtToken);
        var response = await Client.SendAsync(request);
        if (response.IsSuccessStatusCode)
        {
            //Debug.Log("Reward claimed");
        }
        else
        {
            Debug.LogError(response.StatusCode);
            Debug.LogError(response.ReasonPhrase);
            Debug.LogError(response.RequestMessage);
            throw new Exception("Error claiming reward");
        }
    }

    public async UniTask AddExperience(bool win)
    {
        //El endpoint es: `battle-pass/experience`
        var experienceToWin =
            win
                ? ServiceLocator.Instance.GetService<IGlobalSessionService>().GetExperienceToWin()
                : ServiceLocator.Instance.GetService<IGlobalSessionService>().GetExperienceToLose();
        var id = ServiceLocator.Instance.GetService<IPlayerSessionService>().GetPlayerSessionData().playerId;
        var requestUri = ServerUrl + Uri + $"/battle-pass/experience";
        var json = JsonUtility.ToJson(new ExperienceRequest { experience = experienceToWin, playerId = id });
        var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", JwtToken);
        var response = await Client.SendAsync(request);
        if (response.IsSuccessStatusCode)
        {
            //Debug.Log("Experience added");
        }
        else
        {
            Debug.Log(response.StatusCode);
            Debug.Log(response.ReasonPhrase);
            Debug.Log(response.RequestMessage);
            Debug.Log(response.Content.ReadAsStringAsync().Result);
            throw new Exception("Error adding experience");
        }
    }
}

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