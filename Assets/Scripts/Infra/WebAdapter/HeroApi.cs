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
                Debug.LogWarning(
                    $"[HeroApi] GetHeroes non-success status={statusCode} url={requestUri} body={responseData}");
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
            Debug.Log($"[HeroApi] GetJsonWithUnityWebRequestRetry {requestUri}");
            for (int attempt = 1; attempt <= MaxTransientRetries; attempt++)
            {
                using var request = new UnityWebRequest(requestUri, UnityWebRequest.kHttpVerbGET)
                {
                    downloadHandler = new DownloadHandlerBuffer(),
                    timeout = RequestTimeoutSeconds
                };

                request.SetRequestHeader("Accept", "application/json");
                // request.SetRequestHeader("Connection", "close");
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
                        Debug.LogWarning(
                            $"[HeroApi] Exception GET {requestUri} attempt {attempt}/{MaxTransientRetries}: {ex.Message}. Retrying in {delayMs}ms.");
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
                    Debug.LogWarning(
                        $"[HeroApi] Transient GET error {requestUri} attempt {attempt}/{MaxTransientRetries}. Retrying in {delayMs}ms. status={statusCode} result={request.result} error={request.error}");
                    await UniTask.Delay(delayMs);
                    continue;
                }

                Debug.Log(
                    $"[HeroApi] GetJsonWithUnityWebRequestRetry completed attempt {attempt} with status={statusCode} result={responseData}"
                );

                return (statusCode, responseData, false);
            }

            return (-1, string.Empty, true);
        }

        private async UniTask<(long statusCode, string body, bool hasTransportError)> PostJsonWithUnityWebRequestRetry(
            string requestUri,
            string jsonBody,
            bool includeAuth)
        {
            Debug.Log($"[HeroApi] PostJsonWithUnityWebRequestRetry {requestUri} body={jsonBody}");
            for (int attempt = 1; attempt <= MaxTransientRetries; attempt++)
            {
                using var request = new UnityWebRequest(requestUri, UnityWebRequest.kHttpVerbPOST)
                {
                    uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(jsonBody)),
                    downloadHandler = new DownloadHandlerBuffer(),
                    timeout = RequestTimeoutSeconds
                };

                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Accept", "application/json");
                // request.SetRequestHeader("Connection", "close");

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
                        Debug.LogWarning(
                            $"[HeroApi] Exception POST {requestUri} attempt {attempt}/{MaxTransientRetries}: {ex.Message}. Retrying in {delayMs}ms.");
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
                    Debug.LogWarning(
                        $"[HeroApi] Transient POST error {requestUri} attempt {attempt}/{MaxTransientRetries}. Retrying in {delayMs}ms. status={statusCode} result={request.result} error={request.error}");
                    await UniTask.Delay(delayMs);
                    continue;
                }

                Debug.Log(
                    $"[HeroApi] GetJsonWithUnityWebRequestRetry completed attempt {attempt} with status={statusCode} result={responseData}"
                );

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

        // Parser flexible para DialogResponse/DialogData. Intenta varias estrategias de deserialización
        // para tolerar: wrapper { success,data }, objeto data directo, o array top-level.
        private static DialogData ParseDialogFlexible(string responseData)
        {
            if (string.IsNullOrWhiteSpace(responseData))
                return null;

            try
            {
                // 1) Intentar parsear wrapper estándar { success: bool, data: { ... } }
                var wrapped = JsonUtility.FromJson<DialogResponse>(responseData);
                if (wrapped != null && wrapped.success && wrapped.data != null)
                    return wrapped.data;
            }
            catch (Exception)
            {
                // Ignorar y seguir con siguientes estrategias
            }

            try
            {
                // 2) Intentar parsear directamente DialogData (sin wrapper)
                var direct = JsonUtility.FromJson<DialogData>(responseData);
                if (direct != null && (!string.IsNullOrEmpty(direct.dialogId) || direct.id != 0))
                    return direct;
            }
            catch (Exception)
            {
                // Ignorar
            }

            try
            {
                // 3) Si es un array top-level, usar JsonHelper (helper existente en el proyecto)
                if (responseData.TrimStart().StartsWith("[", StringComparison.Ordinal))
                {
                    var list = JsonHelper.FromJson<DialogData>(responseData);
                    if (list != null && list.Count > 0)
                        return list[0];
                }
            }
            catch (Exception)
            {
                // Ignorar
            }

            try
            {
                // 4) Intentar envolver manualmente como {"success":true,"data":<responseData>} y parsear
                var manualWrap = $"{{\"success\":true,\"data\":{responseData}}}";
                var wrapped2 = JsonUtility.FromJson<DialogResponse>(manualWrap);
                if (wrapped2 != null && wrapped2.success && wrapped2.data != null)
                    return wrapped2.data;
            }
            catch (Exception)
            {
                // Ignorar
            }

            return null;
        }

        public async UniTask<DialogData> StartDialogByHero(string heroId, string playerId)
        {
            var requestUri = $"{ServerUrl}{Uri}/heroes/dialog/start";

            // Body que tu backend seguramente espera
            var body = new StartDialogRequest
            {
                heroId = heroId,
                playerId = playerId
            };

            string jsonBody = JsonUtility.ToJson(body);

            var (statusCode, responseData, hasTransportError) =
                await PostJsonWithUnityWebRequestRetry(requestUri, jsonBody, includeAuth: true);

            if (hasTransportError)
            {
                Debug.LogWarning($"[HeroApi] StartDialog transport error after retries. url={requestUri}");
                return null;
            }

            if (statusCode < 200 || statusCode > 299)
            {
                Debug.LogWarning(
                    $"[HeroApi] StartDialog non-success status={statusCode} url={requestUri} body={responseData}");
                return null;
            }

            Debug.Log($"[HeroApi] StartDialog status={statusCode} response: {responseData}");

            try
            {
                var parsed = ParseDialogFlexible(responseData);
                if (parsed == null)
                {
                    Debug.LogWarning("[HeroApi] Invalid dialog response after multiple parse strategies");
                    return null;
                }

                return parsed;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[HeroApi] Error parsing dialog: {ex.Message}");
                return null;
            }
        }

        public async UniTask<AnswerDialogResult> AnswerDialog(
            string playerId,
            string heroId,
            string questionId,
            string answer)
        {
            var uri = $"{ServerUrl}{Uri}/heroes/dialog/answer";

            var body = new AnswerDialogRequest
            {
                playerId = playerId,
                heroId = heroId,
                questionId = questionId,
                answer = answer
            };

            var json = JsonUtility.ToJson(body);

            var (statusCode, responseData, hasTransportError) =
                await PostJsonWithUnityWebRequestRetry(uri, json, true);

            if (hasTransportError || statusCode < 200 || statusCode > 299)
            {
                Debug.LogWarning($"[Dialog] Answer error {statusCode} {responseData}");
                return null;
            }

            var parsed = JsonUtility.FromJson<AnswerDialogResponse>(responseData);

            return parsed?.data;
        }

        [Serializable]
        public class StartDialogRequest
        {
            public string heroId;
            public string playerId;
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

            // 🔥 progreso (estos no estaban antes)
            public int currentXp;
            public int level;
            public int xpToNextLevel;
            public int progressPct;
        }
    }
}