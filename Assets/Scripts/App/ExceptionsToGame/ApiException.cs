using System;
using System.Net;

namespace App.ExceptionsToGame
{
    public class ApiException : Exception
    {
        public HttpStatusCode StatusCode { get; }
        public string ResponseBody { get; }

        public ApiException(string message, HttpStatusCode statusCode, string responseBody = null)
            : base(message)
        {
            StatusCode = statusCode;
            ResponseBody = responseBody;
        }
    }
}