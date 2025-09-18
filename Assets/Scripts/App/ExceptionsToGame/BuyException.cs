using System;

namespace App.ExceptionsToGame
{
    public class BuyException : Exception
    {
        public BuyException()
        {
        }

        public BuyException(string message) : base(message)
        {
        }

        public BuyException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}