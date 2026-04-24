using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Domain.Entities;
using Infra.WebAdapter.Mapper;
using UnityEngine;
using UnityEngine.Networking;

namespace Infra.WebAdapter
{
    public class HeroApi : ApiBase
    {
        private const int MaxTransientRetries = 3;
        private const int RequestTimeoutSeconds = 12;

        public HeroApi(string serverUrl, string uri, IHttpMediator mediator)
            : base(serverUrl, uri, mediator)
        {
        }

        public async UniTask<List<Hero>> GetHeroes(string playerId)
        {
            var requestUri = $"{ServerUrl}{Uri}/heroes/player/{playerId}";

            var (statusCode, responseData, hasTransportError) =
                await GetJsonWithUnityWebRequestRetry(requestUri, includeAuth: true);

            if (hasTransportError)
            {
                Debug.LogWarning($"[HeroApi] GetHeroes transport error after retries. url={requestUri}");
                return new List<Hero>();
            }

            if (statusCode < 200 || statusCode > 299)
            {
                Debug.LogWarning($"[HeroApi] GetHeroes non-success status={statusCode} url={requestUri} body={responseData}");
                return new List<Hero>();
            }

            Debug.Log($"[HeroApi] GetHeroes status={statusCode} response: {responseData}");

            var heroesDto = ParseHeroes(responseData);
            return heroesDto.ConvertAll(HeroMapper.ToDomain);
        }

        private async UniTask<(long statusCode, string body, bool hasTransportError)> GetJsonWithUnityWebRequestRetry(
            string requestUri,
            bool includeAuth)
        {
            for (int attempt = 1; attempt <= MaxTransientRetries; attempt++)
            {
                using var request = new UnityWebRequest(requestUri, UnityWebRequest.kHttpVerbGET)
                {
                    downloadHandler = new DownloadHandlerBuffer(),
                    timeout = RequestTimeoutSeconds
                };

                request.SetRequestHeader("Accept", "application/json");
                request.SetRequestHeader("Connection", "close");
                if (includeAuth && !string.IsNullOrWhiteSpace(JwtToken))
                    request.SetRequestHeader("Authorization", $"Bearer {JwtToken}");

                try
                {
                    await request.SendWebRequest().ToUniTask();
                }
                catch (Exception ex)
                {
                    if (attempt < MaxTransientRetries)
                    {
                        var delayMs = attempt * 1200;
                        Debug.LogWarning($"[HeroApi] Exception GET {requestUri} attempt {attempt}/{MaxTransientRetries}: {ex.Message}. Retrying in {delayMs}ms.");
                        await UniTask.Delay(delayMs);
                        continue;
                    }

                    return (-1, string.Empty, true);
                }

                var statusCode = request.responseCode;
                var responseData = request.downloadHandler?.text ?? string.Empty;

                if (IsTransientUnityError(request, statusCode) && attempt < MaxTransientRetries)
                {
                    var delayMs = attempt * 1200;
                    Debug.LogWarning($"[HeroApi] Transient GET error {requestUri} attempt {attempt}/{MaxTransientRetries}. Retrying in {delayMs}ms. status={statusCode} result={request.result} error={request.error}");
                    await UniTask.Delay(delayMs);
                    continue;
                }

                return (statusCode, responseData, false);
            }

            return (-1, string.Empty, true);
        }

        private static bool IsTransientUnityError(UnityWebRequest request, long statusCode)
        {
            if (request.result == UnityWebRequest.Result.ConnectionError)
                return true;

            if (request.result == UnityWebRequest.Result.DataProcessingError)
                return true;

            return statusCode == 408 || statusCode == 429 || statusCode >= 500;
        }

        private static List<HeroDto> ParseHeroes(string responseData)
        {
            if (string.IsNullOrWhiteSpace(responseData))
                return new List<HeroDto>();

            // Maneja tanto array plano como wrappers tipo ApiResponse.
            if (responseData.TrimStart().StartsWith("[", StringComparison.Ordinal))
                return JsonHelper.FromJson<HeroDto>(responseData) ?? new List<HeroDto>();

            var responseWrapper = JsonUtility.FromJson<HeroResponseDto>(responseData);
            return responseWrapper?.data?.heroes ?? new List<HeroDto>();
        }
    }

    [Serializable]
    public class HeroResponseDto
    {
        public bool success;
        public HeroDataDto data;
    }

    [Serializable]
    public class HeroDataDto
    {
        public List<HeroDto> heroes;
    }

    [Serializable]
    public class HeroMetadataDto
    {
        public string role;
    }

    [Serializable]
    public class HeroDto
    {
        public int id;
        public string heroId;
        public string name;
        public HeroMetadataDto metadata;
        public string created_at;
        public string updated_at;
    }
}

