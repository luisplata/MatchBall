using System;
using System.Net.Http;
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Infra.WebAdapter
{
    public class PlayerApi : ApiBase
    {
        public PlayerApi(string serverUrl, string uri, IHttpMediator mediator)
            : base(serverUrl, uri, mediator)
        {
        }

        public async UniTask<ApiResponse<PlayerDto>> AddPlayer(string playerId, string nickname, string key)
        {
            var json = JsonUtility.ToJson(new AddPlayerRequest
            {
                playerId = playerId,
                nickname = nickname,
                key = key
            });
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var requestUri = $"{ServerUrl}{Uri}/player";

            var response = await Client.PostAsync(requestUri, content);
            var responseData = await response.Content.ReadAsStringAsync();
            Debug.Log($"AddPlayer response: {responseData}");

            if (!response.IsSuccessStatusCode)
                throw new Exception("Failed to add player");

            return JsonUtility.FromJson<ApiResponse<PlayerDto>>(responseData);
        }

        public async UniTask<bool> IsNicknameAvailable(string nickname)
        {
            var requestUri = $"{ServerUrl}{Uri}/player/validate/{nickname}";
            var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", JwtToken);

            var response = await Client.SendAsync(request);
            var responseData = await response.Content.ReadAsStringAsync();
            Debug.Log($"ValidateNickname response: {responseData}");

            if (!response.IsSuccessStatusCode)
                return false;

            var parsed = JsonUtility.FromJson<ApiResponse<NicknameValidationDto>>(responseData);
            return parsed.data.available;
        }

        public async UniTask<string> GetPlayerIdByNickname(string nickname)
        {
            var requestUri = $"{ServerUrl}{Uri}/player/{nickname}";
            var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", JwtToken);

            var response = await Client.SendAsync(request);
            var responseData = await response.Content.ReadAsStringAsync();
            Debug.Log($"GetPlayerId response: {responseData}");

            if (!response.IsSuccessStatusCode)
                return null;

            var parsed = JsonUtility.FromJson<ApiResponse<PlayerIdResponseDto>>(responseData);
            return parsed.data.playerId;
        }

        public async UniTask<ApiResponse<object>> UpdatePlayerNickname(string playerId, string nickname)
        {
            var json = JsonUtility.ToJson(new UpdateNicknameRequest
            {
                nickname = nickname
            });
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var requestUri = $"{ServerUrl}{Uri}/player/nickname/{playerId}";

            var request = new HttpRequestMessage(HttpMethod.Put, requestUri) { Content = content };
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", JwtToken);

            var response = await Client.SendAsync(request);
            var responseData = await response.Content.ReadAsStringAsync();
            Debug.Log($"UpdateNickname response: {responseData}");

            if (!response.IsSuccessStatusCode)
                throw new Exception("Failed to update nickname");

            return JsonUtility.FromJson<ApiResponse<object>>(responseData);
        }

        public async UniTask<string> LoginPlayer(string playerId)
        {
            var requestDto = new LoginPlayerRequest { playerId = playerId };
            var json = JsonUtility.ToJson(requestDto);

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var requestUri = $"{ServerUrl}{Uri}/player/login";

            var response = await Client.PostAsync(requestUri, content);
            var responseData = await response.Content.ReadAsStringAsync();
            Debug.Log($"LoginPlayer response: {responseData}");

            if (!response.IsSuccessStatusCode)
                throw new Exception("Failed to login player");

            var parsed = JsonUtility.FromJson<ApiResponse<TokenResponseDto>>(responseData);
            var token = parsed.data.token;
            _mediator.UpdateJwtToken(token);
            return token;
        }

        public async UniTask<PlayerDto> GetPlayerById(string playerId)
        {
            var requestUri = $"{ServerUrl}{Uri}/player/id/{playerId}";
            var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", JwtToken);

            var response = await Client.SendAsync(request);
            var responseData = await response.Content.ReadAsStringAsync();
            Debug.Log($"GetPlayerById response: {responseData}");

            if (!response.IsSuccessStatusCode)
                return null;

            var parsed = JsonUtility.FromJson<ApiResponse<PlayerDto>>(responseData);
            return parsed.data;
        }
    }

    [Serializable]
    public class ApiResponse<T>
    {
        public bool success;
        public string message;
        public T data;
    }

    [Serializable]
    public class PlayerDto
    {
        public string playerId;
        public string nickname;
        public string created_at;
        public string updated_at;
    }

    [Serializable]
    public class NicknameValidationDto
    {
        public bool available;
    }

    [Serializable]
    public class PlayerIdResponseDto
    {
        public string playerId;
    }

    [Serializable]
    public class TokenResponseDto
    {
        public string token;
        public PlayerDto player;
    }

    [Serializable]
    public class AddPlayerRequest
    {
        public string playerId;
        public string nickname;
        public string key;
    }

    [Serializable]
    public class LoginPlayerRequest
    {
        public string playerId;
    }

    [Serializable]
    public class UpdateNicknameRequest
    {
        public string nickname;
    }
}