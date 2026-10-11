using System;
using TrafficOptimizer.Services;
using TrafficOptimizer.Utils;
using TrafficOptimizer.Patches;

namespace TrafficOptimizer.Core
{
    /// <summary>
    /// Центральный запуск TrafficOptimizer.
    /// Отвечает за правильный порядок инициализации компонентов.
    /// Исправленная версия: добавлена загрузка Config, улучшен порядок.
    /// </summary>
    public static class Bootstrap
    {
        private static bool _initialized;


        /// <summary>
        /// Запуск всех систем мода.
        /// </summary>
        public static void Initialize()
        {
            if (_initialized)
            {
                Logger.Warning("Bootstrap already initialized.");
                return;
            }

            try
            {
                // 1. Сначала логгер (чтобы все остальные могли писать логи)
                Logger.Initialize();

                Logger.Info(
                    VersionInfo.FullName + " starting...");


                // 2. Загрузка конфигурации
                Config.Load();

                Logger.Info(
                    "Config loaded.");

                // Применяем DebugMode из конфига
                GameState.DebugMode = Config.Data.DebugMode;


                // 3. Инициализация состояния
                GameState.Initialize();


                Logger.Info(
                    "Core systems initialized.");


                // 4. Harmony — патчи для изменения поведения игры
                HarmonyBootstrap.Initialize();
                

                
                if (GameState.HarmonyInitialized)
                {
                    Logger.Info(
                        "Harmony system initialized.");
                }



                // 5. Утилиты анализа
                RoadAnalyzer.Initialize();

                VehicleAnalyzer.Initialize();



                // 6. Сервисы
                ServiceManager.Initialize();


                _initialized = true;


                Logger.Info(
                    "TrafficOptimizer startup completed.");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex);

                // Не перезапускаем исключение — игра не должна падать
                // из-за ошибки в моде
                Logger.Error("TrafficOptimizer failed to initialize, but game will continue.");
            }
        }


        /// <summary>
        /// Корректное завершение работы мода.
        /// </summary>
        public static void Shutdown()
        {
            if (!_initialized)
                return;


            try
            {
                Logger.Info(
                    "Stopping TrafficOptimizer...");

                ServiceManager.Shutdown();


                RoadAnalyzer.Shutdown();


                VehicleAnalyzer.Shutdown();


                HarmonyBootstrap.Shutdown();


                Config.Save();


                Logger.Shutdown();


                _initialized = false;
            }
            catch (Exception ex)
            {
                Logger.Exception(ex);
            }
        }
    }
}
