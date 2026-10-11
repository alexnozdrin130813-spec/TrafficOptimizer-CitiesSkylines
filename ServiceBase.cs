using System;

namespace TrafficOptimizer.Services
{
    /// <summary>
    /// Базовый класс для всех сервисов TrafficOptimizer.
    /// Каждый сервис имеет одинаковый жизненный цикл.
    /// </summary>
    public abstract class ServiceBase
    {

        /// <summary>
        /// Название сервиса.
        /// </summary>
        public abstract string Name { get; }


        /// <summary>
        /// Был ли сервис запущен.
        /// </summary>
        public bool IsInitialized
        {
            get;
            private set;
        }



        /// <summary>
        /// Инициализация сервиса.
        /// </summary>
        public void Initialize()
        {
            if (IsInitialized)
                return;


            try
            {
                OnInitialize();

                IsInitialized = true;
            }
            catch (Exception ex)
            {
                Core.Logger.Exception(ex);
            }
        }



        /// <summary>
        /// Обновление сервиса.
        /// </summary>
        public void Update()
        {
            if (!IsInitialized)
                return;


            try
            {
                OnUpdate();
            }
            catch (Exception ex)
            {
                Core.Logger.Exception(ex);
            }
        }



        /// <summary>
        /// Завершение работы сервиса.
        /// </summary>
        public void Shutdown()
        {
            if (!IsInitialized)
                return;


            try
            {
                OnShutdown();

                IsInitialized = false;
            }
            catch (Exception ex)
            {
                Core.Logger.Exception(ex);
            }
        }



        /// <summary>
        /// Код запуска конкретного сервиса.
        /// </summary>
        protected abstract void OnInitialize();



        /// <summary>
        /// Код обновления конкретного сервиса.
        /// </summary>
        protected abstract void OnUpdate();



        /// <summary>
        /// Код остановки конкретного сервиса.
        /// </summary>
        protected abstract void OnShutdown();

    }
}