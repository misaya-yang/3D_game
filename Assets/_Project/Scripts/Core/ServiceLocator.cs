using System;
using System.Collections.Generic;

namespace Wendao.Core
{
    /// <summary>
    /// Exact-type service registry for cross-system queries.
    /// State changes must still be published through <see cref="EventBus"/>.
    /// </summary>
    public static class ServiceLocator
    {
        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<Type, object> Services = new Dictionary<Type, object>();

        public static void Register<T>(T service) where T : class
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }

            var serviceType = typeof(T);
            lock (SyncRoot)
            {
                if (Services.TryGetValue(serviceType, out var existing))
                {
                    if (ReferenceEquals(existing, service))
                    {
                        return;
                    }

                    throw new InvalidOperationException(
                        $"A service is already registered for {serviceType.FullName}.");
                }

                Services.Add(serviceType, service);
            }
        }

        public static void Unregister<T>() where T : class
        {
            lock (SyncRoot)
            {
                Services.Remove(typeof(T));
            }
        }

        public static T Get<T>() where T : class
        {
            if (TryGet<T>(out var service))
            {
                return service;
            }

            throw new InvalidOperationException(
                $"No service is registered for {typeof(T).FullName}.");
        }

        public static bool TryGet<T>(out T service) where T : class
        {
            lock (SyncRoot)
            {
                if (Services.TryGetValue(typeof(T), out var registered))
                {
                    service = (T)registered;
                    return true;
                }
            }

            service = null;
            return false;
        }

        public static void Clear()
        {
            lock (SyncRoot)
            {
                Services.Clear();
            }
        }
    }
}

