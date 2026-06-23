using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Domain.Entities;
using Infra.WebAdapter.Mapper;
using UnityEngine;
using UnityEngine.Networking;

namespace Infra.WebAdapter
{
    public class PlayerRewardApi : ApiBase
    {
        private const int MaxTransientRetries = 3;
        private const int RequestTimeoutSeconds = 12;

        public PlayerRewardApi(string serverUrl, string uri, IHttpMediator mediator)
            : base(serverUrl, uri, mediator)
        {
        }

        public async UniTask<List<RewardData>> GetRewards()
        {
            var requestUri = $"{ServerUrl}{Uri}/battle-pass-reward";
            // var (statusCode, responseData, hasTransportError) = await SendJsonWithUnityWebRequestRetry(requestUri, UnityWebRequest.kHttpVerbGET, null, includeAuth: true);

            // if (hasTransportError || statusCode < 200 || statusCode > 299)
            //     return new List<RewardData>();
            //
            // Debug.Log($"Rewards response: {responseData}");
            //
            // // Como es un array, usamos wrapper
            // var listWrapper = new RewardDtoListWrapper { rewards = JsonHelper.FromJson<RewardDto>(responseData) };
            // return listWrapper.rewards.ConvertAll(PlayerRewardMapper.ToDomain);
            return new List<RewardData>();
        }

        public async UniTask<PlayerPassBall> GetPlayerPassBall()
        {
            // var id = ServiceLocator.Instance.GetService<IPlayerSessionService>()
            //     .GetPlayerSessionData().playerId;
            // var requestUri = $"{ServerUrl}{Uri}/battle-pass/{id}";
            //
            // var (statusCode, responseData, hasTransportError) =
            //     await SendJsonWithUnityWebRequestRetry(requestUri, UnityWebRequest.kHttpVerbGET, null, includeAuth: true);
            //
            // if (hasTransportError)
            //     throw new Exception("Error getting player pass ball: transport error");
            // if (statusCode < 200 || statusCode > 299)
            //     throw new Exception($"Error getting player pass ball {statusCode}");
            //
            // Debug.Log($"PassBall response: {responseData}");
            //
            // var wrapper = JsonUtility.FromJson<PlayerPassBallResponseDto>(responseData);
            //
            // // Mapear usando tu PlayerRewardMapper
            // return PlayerRewardMapper.ToDomain(wrapper.data.battlePass, wrapper.data.rewards);
            return new PlayerPassBall(
                0,
                "playerId",
                0,
                0,
                DateTime.UtcNow,
                DateTime.UtcNow,
                new List<PlayerReward>()
            );
        }

        public async UniTask<ClaimRewardResponse> ClaimReward(int rewardDataLevel)
        {
            // var id = ServiceLocator.Instance
            //     .GetService<IPlayerSessionService>()
            //     .GetPlayerSessionData().playerId;
            //
            // var requestUri = $"{ServerUrl}{Uri}/player-reward/claim";
            //
            // var json = JsonUtility.ToJson(new ClaimRewardRequest
            // {
            //     playerId = id,
            //     level = rewardDataLevel
            // });
            //
            // var (statusCode, responseData, hasTransportError) =
            //     await SendJsonWithUnityWebRequestRetry(requestUri, UnityWebRequest.kHttpVerbPOST, json,
            //         includeAuth: true);
            //
            // Debug.Log($"ClaimReward response: {responseData}");
            //
            // if (hasTransportError)
            //     throw new Exception("Error claiming reward: transport error");
            // if (statusCode < 200 || statusCode > 299)
            //     throw new Exception($"Error claiming reward: {statusCode}");
            //
            // return JsonUtility.FromJson<ClaimRewardResponse>(responseData);
            return new ClaimRewardResponse
            {
                message = "Reward claimed successfully"
            };
        }

        public async UniTask<PlayerPassBall> AddExperience(bool win)
        {
            // var experienceToWin = win
            //     ? ServiceLocator.Instance.GetService<IGlobalSessionService>().GetExperienceToWin()
            //     : ServiceLocator.Instance.GetService<IGlobalSessionService>().GetExperienceToLose();
            //
            // var id = ServiceLocator.Instance.GetService<IPlayerSessionService>()
            //     .GetPlayerSessionData().playerId;
            // var requestUri = $"{ServerUrl}{Uri}/battle-pass/experience";
            //
            // var json = JsonUtility.ToJson(new ExperienceRequest { experience = experienceToWin, playerId = id });
            // var (statusCode, responseData, hasTransportError) =
            //     await SendJsonWithUnityWebRequestRetry(requestUri, UnityWebRequest.kHttpVerbPOST, json,
            //         includeAuth: true);
            //
            // if (hasTransportError)
            //     throw new Exception("Error adding experience: transport error");
            // if (statusCode < 200 || statusCode > 299)
            //     throw new Exception($"Error adding experience: {statusCode}");
            //
            // Debug.Log($"AddExperience response: {responseData}");
            //
            // var wrapper = JsonUtility.FromJson<AddExperienceResponseDto>(responseData);
            // var dto = wrapper.data;
            //
            // return new PlayerPassBall(
            //     dto.id,
            //     dto.playerId,
            //     dto.level,
            //     dto.experience,
            //     DateTime.Parse(dto.created_at, null, System.Globalization.DateTimeStyles.RoundtripKind),
            //     DateTime.Parse(dto.updated_at, null, System.Globalization.DateTimeStyles.RoundtripKind),
            //     new List<PlayerReward>() // por ahora vacío; rewards se piden con otro endpoint
            // );
            return new PlayerPassBall(
                0,
                "playerId",
                0,
                0,
                DateTime.UtcNow,
                DateTime.UtcNow,
                new List<PlayerReward>()
            );
        }

        private async UniTask<(long statusCode, string body, bool hasTransportError)> SendJsonWithUnityWebRequestRetry(
            string requestUri,
            string method,
            string jsonPayload,
            bool includeAuth)
        {
            for (int attempt = 1; attempt <= MaxTransientRetries; attempt++)
            {
                using var request = new UnityWebRequest(requestUri, method)
                {
                    downloadHandler = new DownloadHandlerBuffer(),
                    timeout = RequestTimeoutSeconds
                };

                if (!string.IsNullOrEmpty(jsonPayload))
                {
                    request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(jsonPayload));
                    request.SetRequestHeader("Content-Type", "application/json");
                }

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
                        Debug.LogWarning(
                            $"[PlayerRewardApi] Exception {method} {requestUri} attempt {attempt}/{MaxTransientRetries}: {ex.Message}. Retrying in {delayMs}ms.");
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
                        $"[PlayerRewardApi] Transient {method} error {requestUri} attempt {attempt}/{MaxTransientRetries}. Retrying in {delayMs}ms. status={statusCode} result={request.result} error={request.error}");
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
        public int level; // <-- agregado
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