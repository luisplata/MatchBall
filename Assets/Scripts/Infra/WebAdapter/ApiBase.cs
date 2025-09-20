using System.Net.Http;

namespace Infra.WebAdapter
{
    public abstract class ApiBase
    {
        protected readonly HttpClient Client;
        protected readonly string ServerUrl;
        protected readonly string Uri;
        public string JwtToken;
        protected readonly IHttpMediator _mediator;

        protected ApiBase(string serverUrl, string uri, IHttpMediator mediator)
        {
            Client = new HttpClient();
            ServerUrl = serverUrl;
            Uri = uri;
            _mediator = mediator;
        }
    }
}