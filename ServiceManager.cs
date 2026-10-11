using System;
using System.Collections.Generic;
using TrafficOptimizer.Core;

namespace TrafficOptimizer.Services
{
    /// <summary>
    /// Управляет всеми сервисами TrafficOptimizer.
    /// </summary>
    public static class ServiceManager
    {
        private static readonly List<ServiceBase> _services =
            new List<ServiceBase>();


        private static bool _initialized;


        /// <summary>
        /// Регистрация сервисов.
        /// </summary>
        public static void Initialize()
        {
            if (_initialized)
                return;


            Logger.Info("Initializing services...");


            RegisterServices();


            foreach (ServiceBase service in _services)
            {
                service.Initialize();


                ServiceLocator.Register(
                    service);
            }


            _initialized = true;


            Logger.Info(
                "Services initialized: " + _services.Count + "");
        }



        private static void RegisterServices()
        {
            _services.Clear();


            _services.Add(
                new TrafficService());


            _services.Add(
                new DecisionEngine());


            _services.Add(
                new StatisticsService());

            _services.Add(
                new JunctionConnectionService());
        }



        /// <summary>
        /// Обновление всех сервисов.
        /// </summary>
        public static void UpdateAll()
        {
            if (!_initialized)
                return;


            foreach (var service in _services)
            {
                service.Update();
            }
        }



        /// <summary>
        /// Остановка всех сервисов.
        /// </summary>
        public static void Shutdown()
        {
            if (!_initialized)
                return;


            Logger.Info(
                "Shutting down services...");


            foreach (var service in _services)
            {
                service.Shutdown();
            }


            _services.Clear();

            _initialized = false;
        }
    }
}