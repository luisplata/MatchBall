using System;

namespace Domain.Entities
{
    [Serializable]
    public class ApiResponse<T>
    {
        public bool success;
        public string message;
        public T data;
    }
}