using System;
using System.Net;
using System.Net.Http;
using System.Text;
using Cysharp.Threading.Tasks;
using Domain.Entities;
using UnityEngine;
using UnityEngine.Networking;

namespace Infra.WebAdapter
{
    public class PlayerApi : ApiBase
    {
        private const int MaxTransientRetries = 3;
        private const int RequestTimeoutSeconds = 12;

        public PlayerApi(string serverUrl, string uri, IHttpMediator mediator)
            : base(serverUrl, uri, mediator)
        {
        }

        public async UniTask<ApiResponse<PlayerDto>> AddPlayer(string playerId, string nickname, string key)
        {
            // Validate input locally to avoid sending requests that the server will reject
            if (string.IsNullOrWhiteSpace(playerId) || string.IsNullOrWhiteSpace(nickname))
            {
                Debug.LogWarning(
                    $"[PlayerApi] AddPlayer called with empty playerId or nickname. playerId='{playerId}' nickname='{nickname}'");
                return new ApiResponse<PlayerDto>
                {
                    success = false,
                    message = "playerId and nickname are required",
                    data = null
                };
            }

            // var requestUri = $"{ServerUrl}{Uri}/player";
            // var payload = new AddPlayerRequest
            // {
            //     playerId = playerId,
            //     nickname = nickname,
            //     key = key
            // };
            //
            // Debug.Log($"[PlayerApi] url {requestUri} payload: {JsonUtility.ToJson(payload)}");
            //
            // var (statusCode, responseData) = await SendJsonWithUnityWebRequestRetry(
            //     requestUri,
            //     UnityWebRequest.kHttpVerbPOST,
            //     payload,
            //     includeAuth: false);
            // Debug.Log($"[PlayerApi] AddPlayer status={statusCode} response: {responseData}");
            //
            // if (statusCode == (long)HttpStatusCode.Conflict)
            // {
            //     Debug.LogWarning("[PlayerApi] AddPlayer returned 409 (already exists). Continuing as idempotent success.");
            //     return new ApiResponse<PlayerDto>
            //     {
            //         success = true,
            //         message = "Player already exists",
            //         data = null
            //     };
            // }
            //
            // EnsureSuccessOrThrow("add player", requestUri, statusCode, responseData);
            // return JsonUtility.FromJson<ApiResponse<PlayerDto>>(responseData);
            var response = new ApiResponse<PlayerDto>()
            {
                message = "Ok",
                data = new PlayerDto
                {
                    playerId = playerId,
                    nickname = nickname,
                    created_at = DateTime.UtcNow.ToString("o"),
                    updated_at = DateTime.UtcNow.ToString("o")
                },
                success = true
            };
            return response;
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
            // var requestUri = $"{ServerUrl}{Uri}/player/nickname/{playerId}";
            // var payload = new UpdateNicknameRequest { nickname = nickname };
            //
            // var (statusCode, responseData) = await SendJsonWithUnityWebRequestRetry(
            //     requestUri,
            //     UnityWebRequest.kHttpVerbPUT,
            //     payload,
            //     includeAuth: true);
            //
            // Debug.Log($"[PlayerApi] UpdateNickname status={statusCode} response: {responseData}");
            // EnsureSuccessOrThrow("update nickname", requestUri, statusCode, responseData);
            //
            // return JsonUtility.FromJson<ApiResponse<object>>(responseData);
            return new ApiResponse<object>
            {
                success = true,
                message = "Nickname updated successfully",
                data = null
            };
        }

        public async UniTask<string> LoginPlayer(string playerId)
        {
            var requestDto = new LoginPlayerRequest { playerId = playerId };
            var requestUri = $"{ServerUrl}{Uri}/player/login";
            //
            // Debug.Log($"[PlayerApi] url {requestUri} payload: {JsonUtility.ToJson(requestDto)}");
            //
            // var (statusCode, responseData) = await SendJsonWithUnityWebRequestRetry(
            //     requestUri,
            //     UnityWebRequest.kHttpVerbPOST,
            //     requestDto,
            //     includeAuth: false);
            // Debug.Log($"[PlayerApi] LoginPlayer status={statusCode} response: {responseData}");
            // EnsureSuccessOrThrow("login player", requestUri, statusCode, responseData);
            //
            // var parsed = JsonUtility.FromJson<ApiResponse<TokenResponseDto>>(responseData);
            // var token = parsed.data.token;
            var token = playerId;
            _mediator.UpdateJwtToken(token);
            return token;
        }

        private async UniTask<(long statusCode, string body)> SendJsonWithUnityWebRequestRetry(
            string requestUri,
            string method,
            object payload,
            bool includeAuth)
        {
            Exception lastException = null;

            for (int attempt = 1; attempt <= MaxTransientRetries; attempt++)
            {
                try
                {
                    var json = JsonUtility.ToJson(payload);
                    using var request = new UnityWebRequest(requestUri, method)
                    {
                        uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)),
                        downloadHandler = new DownloadHandlerBuffer(),
                        timeout = RequestTimeoutSeconds
                    };

                    // Unity manages some headers (Connection etc.) internally; avoid setting them manually
                    request.SetRequestHeader("Content-Type", "application/json");
                    request.SetRequestHeader("Accept", "application/json");
                    if (includeAuth && !string.IsNullOrWhiteSpace(JwtToken))
                        request.SetRequestHeader("Authorization", $"Bearer {JwtToken}");

                    await request.SendWebRequest().ToUniTask();

                    var statusCode = request.responseCode;
                    var responseData = request.downloadHandler?.text ?? string.Empty;

                    if (IsTransientUnityError(request, statusCode) && attempt < MaxTransientRetries)
                    {
                        var delayMs = attempt * 1200;
                        Debug.LogWarning(
                            $"[PlayerApi] Transient network error {method} {requestUri} attempt {attempt}/{MaxTransientRetries}. Retrying in {delayMs}ms. error={request.error}");
                        await UniTask.Delay(delayMs);
                        continue;
                    }

                    return (statusCode, responseData);
                }
                catch (Exception ex) when (attempt < MaxTransientRetries)
                {
                    lastException = ex;
                    var delayMs = attempt * 1200;
                    Debug.LogWarning(
                        $"[PlayerApi] Exception {method} {requestUri} attempt {attempt}/{MaxTransientRetries}: {ex.Message}. Retrying in {delayMs}ms.");
                    await UniTask.Delay(delayMs);
                }
            }

            throw new Exception($"HTTP request failed after {MaxTransientRetries} attempts: {method} {requestUri}",
                lastException);
        }

        private static bool IsTransientUnityError(UnityWebRequest request, long statusCode)
        {
            if (request.result == UnityWebRequest.Result.ConnectionError)
                return true;

            if (request.result == UnityWebRequest.Result.DataProcessingError)
                return true;

            return statusCode == 408 || statusCode == 429 || statusCode >= 500;
        }

        private static void EnsureSuccessOrThrow(
            string operation,
            string requestUri,
            long statusCode,
            string responseData)
        {
            if ((int)statusCode >= 200 && (int)statusCode <= 299)
                return;

            throw new Exception(
                $"Failed to {operation}. Status={(int)statusCode} Url={requestUri} Body={responseData}");
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