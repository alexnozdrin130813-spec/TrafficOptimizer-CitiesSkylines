using System;
using System.Collections.Generic;

namespace TrafficOptimizer.Core
{
    /// <summary>
    /// Глобальный доступ к сервисам.
    /// </summary>
    public static class ServiceLocator
    {

        private static readonly Dictionary<Type, object> _services =
            new Dictionary<Type, object>();



        public static void Register<T>(
            T service)
            where T : class
        {
            Type type = typeof(T);


            if (_services.ContainsKey(type))
                return;


            _services.Add(
                type,
                service);
        }



        public static T Get<T>()
            where T : class
        {
            object service;


            if (_services.TryGetValue(
                typeof(T),
                out service))
            {
                return service as T;
            }


            return null;
        }



        public static void Clear()
        {
            _services.Clear();
        }

    }
}