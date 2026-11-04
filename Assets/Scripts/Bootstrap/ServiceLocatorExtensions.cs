using System;

namespace Bootstrap
{
    public static class ServiceLocatorExtensions
    {
        public static bool TryGetService<T>(this ServiceLocator locator, out T service)
        {
            try
            {
                service = locator.GetService<T>();
                return true;
            }
            catch (Exception)
            {
                service = default;
                return false;
            }
        }
    }
}
