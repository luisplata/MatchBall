using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using Cysharp.Threading.Tasks;
using Domain.Entities;
using Infra.WebAdapter.Mapper;
using UnityEngine;

namespace Infra.WebAdapter
{
    public class HeroApi : ApiBase
    {
        public HeroApi(string serverUrl, string uri, IHttpMediator mediator)
            : base(serverUrl, uri, mediator)
        {
        }

        public async UniTask<List<Hero>> GetHeroes()
        {
            var requestUri = $"{ServerUrl}{Uri}/heroes";
            var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", JwtToken);

            var response = await Client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return new List<Hero>();

            var responseData = await response.Content.ReadAsStringAsync();
            Debug.Log($"Heroes response: {responseData}");

            var heroesDto = ParseHeroes(responseData);
            return heroesDto.ConvertAll(HeroMapper.ToDomain);
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

