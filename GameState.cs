using System;

namespace TrafficOptimizer.Core
{
    /// <summary>
    /// Хранит текущее состояние TrafficOptimizer.
    /// </summary>
    public static class GameState
    {
        /// <summary>
        /// Загружен ли город.
        /// </summary>
        public static bool CityLoaded { get; internal set; }

        /// <summary>
        /// Инициализирован ли Harmony.
        /// </summary>
        public static bool HarmonyInitialized { get; internal set; }

        /// <summary>
        /// Загружена ли панель статистики.
        /// </summary>
        public static bool UIInitialized { get; internal set; }

        /// <summary>
        /// Включён ли режим отладки.
        /// </summary>
        public static bool DebugMode { get; set; }

        /// <summary>
        /// Текущий игровой кадр.
        /// </summary>
        public static ulong SimulationFrame { get; internal set; }

        /// <summary>
        /// Время последнего анализа трафика.
        /// </summary>
        public static DateTime LastTrafficScan { get; internal set; }

        /// <summary>
        /// Время запуска мода.
        /// </summary>
        public static DateTime StartupTime { get; private set; }

        public static DateTime CityLoadedTime { get; internal set; }

        /// <summary>
        /// Инициализация состояния.
        /// </summary>
        public static void Initialize()
        {
            StartupTime = DateTime.Now;
            CityLoadedTime = DateTime.MinValue;
            LastTrafficScan = DateTime.MinValue;

            CityLoaded = false;
            HarmonyInitialized = false;
            UIInitialized = false;
            DebugMode = false;
            SimulationFrame = 0;

            Logger.Info("GameState initialized.");
        }

        /// <summary>
        /// Обновление номера кадра.
        /// </summary>
        public static void UpdateFrame(ulong frame)
        {
            SimulationFrame = frame;
        }

        /// <summary>
        /// Отмечает выполнение анализа трафика.
        /// </summary>
        public static void MarkTrafficScan()
        {
            LastTrafficScan = DateTime.Now;
        }

        /// <summary>
        /// Возвращает время работы мода.
        /// </summary>
        public static TimeSpan GetUptime()
        {
            return DateTime.Now - StartupTime;
        }
    }
}