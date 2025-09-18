using System;
using System.Net.Http;
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class PlayerApi : ApiBase
{
    public PlayerApi(string serverUrl, string uri, IHttpMediator mediator)
        : base(serverUrl, uri, mediator)
    {
    }

    public async UniTask AddPlayer(string playerId, string nickname, string key)
    {
        try
        {
            var json = $"{{\"playerId\": \"{playerId}\", \"nickname\": \"{nickname}\", \"key\": \"{key}\"}}";
            //Debug.Log("JSON Payload: " + json); // Log the JSON payload

            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var requestUri = ServerUrl + Uri + "/player";
            //Debug.Log("Request URI: " + requestUri);
            var response = await Client.PostAsync(requestUri, content);
            if (response.IsSuccessStatusCode)
            {
                var responseData = await response.Content.ReadAsStringAsync();
                //Debug.Log("Player added: " + responseData);
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                Debug.LogError("Nickname or Player ID already exists");
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                Debug.LogError("Unauthorized");
            }
            else
            {
                Debug.LogError("Failed to add player");
            }
            //Debug.Log("Response: " + response);
        }
        catch (Exception e)
        {
            //Debug.LogError(e);
            ServiceLocator.Instance.GetService<IMessageService>().ErrorMessage($"Error adding player: {e.Message}", 3);
        }
    }

    public async UniTask<bool> IsNicknameAvailable(string nickname)
    {
        var requestUri = $"{ServerUrl}{Uri}/player/validate/{nickname}";
        //Debug.Log("Request URI: " + requestUri);

        var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", JwtToken);

        var response = await Client.SendAsync(request);
        if (response.IsSuccessStatusCode)
        {
            var isAvailable = bool.Parse(await response.Content.ReadAsStringAsync());
            //Debug.Log("Is nickname available: " + isAvailable);
            return isAvailable;
        }
        else
        {
            Debug.LogError("Failed to validate nickname");
            return false;
        }
    }

    public async UniTask<int?> GetPlayerIdByNickname(string nickname)
    {
        var requestUri = $"{ServerUrl}{Uri}/player/{nickname}";
        //Debug.Log("Request URI: " + requestUri);

        var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", JwtToken);

        var response = await Client.SendAsync(request);
        if (response.IsSuccessStatusCode)
        {
            var playerId = int.Parse(await response.Content.ReadAsStringAsync());
            //Debug.Log("Player ID: " + playerId);
            return playerId;
        }
        else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            var error = await response.Content.ReadAsStringAsync();
            Debug.LogError("Error: " + error);
            return null;
        }
        else
        {
            Debug.LogError("Failed to get player ID");
            return null;
        }
    }

    public async UniTask UpdatePlayerNickname(string playerId, string nickname)
    {
        try
        {
            var json = $"{{\"nickname\": \"{nickname}\"}}";
            //Debug.Log("JSON Payload: " + json); // Log the JSON payload

            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var requestUri = $"{ServerUrl}{Uri}/player/nickname/{playerId}";
            //Debug.Log("Request URI: " + requestUri);

            var request = new HttpRequestMessage(HttpMethod.Put, requestUri)
            {
                Content = content
            };
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", JwtToken);

            var response = await Client.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                var responseData = await response.Content.ReadAsStringAsync();
                //Debug.Log("Nickname updated: " + responseData);
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                Debug.LogError("Player not found");
            }
            else
            {
                Debug.LogError("Failed to update nickname");
            }
        }
        catch (Exception e)
        {
            ServiceLocator.Instance.GetService<IMessageService>().ErrorMessage($"Error Logging in: {e.Message} ", 3);
        }
    }

    public async UniTask<string> LoginPlayer(string playerId)
    {
        try
        {
            var json = $"{{\"playerId\": \"{playerId}\"}}";
            //Debug.Log("JSON Payload: " + json); // Log the JSON payload

            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var requestUri = $"{ServerUrl}{Uri}/player/login";
            //Debug.Log("Request URI: " + requestUri);
            var response = await Client.PostAsync(requestUri, content);
            //Debug.Log("Response: " + response);
            if (response.IsSuccessStatusCode)
            {
                var responseData = await response.Content.ReadAsStringAsync();
                var token = JsonUtility.FromJson<TokenResponse>(responseData).token;
                //Debug.Log("Login successful, token: " + token);
                _mediator.UpdateJwtToken(token);
                return token;
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                Debug.LogError("Invalid playerId");
                throw new Exception("Invalid playerId");
            }
            else
            {
                Debug.LogError("Failed to authenticate player");
                throw new Exception("Failed to authenticate player");
            }
        }
        catch (Exception e)
        {
            ServiceLocator.Instance.GetService<IMessageService>().ErrorMessage($"Error Logging in: {e.Message} ", 3);
            return string.Empty;
        }
    }

    public async UniTask<PlayerData> GetPlayerById(string playerId)
    {
        try
        {
            var requestUri = $"{ServerUrl}{Uri}/player/id/{playerId}";
            //Debug.Log("Request URI: " + requestUri);

            var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", JwtToken);

            var response = await Client.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                var responseData = await response.Content.ReadAsStringAsync();
                var player = JsonUtility.FromJson<PlayerData>(responseData);
                //Debug.Log("Player retrieved: " + player);
                return player;
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                Debug.LogError("Player not found");
                return null;
            }
            else
            {
                Debug.LogError("Failed to get player");
                return null;
            }
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            return new PlayerData();
        }
    }
}