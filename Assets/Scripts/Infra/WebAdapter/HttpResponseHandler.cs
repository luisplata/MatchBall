using Cysharp.Threading.Tasks;
using System.Net.Http;

namespace Infra.WebAdapter
{
    public static class HttpResponseHandler
    {
        public static async UniTask<string> EnsureSuccess(HttpResponseMessage response, string contextMessage)
        {
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync();
            }

            var errorBody = await response.Content.ReadAsStringAsync();
            throw new App.ExceptionsToGame.ApiException(
                $"{contextMessage}: {response.ReasonPhrase}",
                response.StatusCode,
                errorBody
            );
        }
    }
}