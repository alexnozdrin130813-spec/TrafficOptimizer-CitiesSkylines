namespace TrafficOptimizer.Core
{
    /// <summary>
    /// Настройки TrafficOptimizer.
    /// Все значения автоматически сохраняются в XML.
    /// </summary>
    public class ConfigData
    {
        /// <summary>
        /// Включить статистику.
        /// </summary>
        public bool EnableStatistics { get; set; }

        /// <summary>
        /// Включить обнаружение узких мест.
        /// </summary>
        public bool EnableBottleneckDetection { get; set; }

        /// <summary>
        /// Режим разработчика.
        /// </summary>
        public bool DebugMode { get; set; }

        /// <summary>
        /// Интервал анализа трафика (секунды).
        /// </summary>
        public int ScanInterval { get; set; }

        /// <summary>
        /// Режим глубины сканирования трафика.
        /// Нормально = до 10000 машин, Средне = до 25000 машин.
        /// Больше сканирование = выше точность, но ниже FPS.
        /// </summary>
        public int ScanDepthMode { get; set; }

        /// <summary>
        /// Включить адаптивное снижение глубины скана при низком FPS.
        /// </summary>
        public bool EnableAdaptiveScanning { get; set; }

        /// <summary>
        /// Целевой FPS для адаптивного режима.
        /// </summary>
        public int MinFpsThreshold { get; set; }

        /// <summary>
        /// Захват игрового лога (DebugOutputPanel) в файл.
        /// Сохраняет только сообщения TrafficOptimizer и все Error.
        /// </summary>
        public bool EnableGameLogCapture { get; set; }

        /// <summary>
        /// Задержка перед началом тяжёлых сканов после запуска мода (секунды).
        /// Полезно для сокращения времени загрузки игры.
        /// </summary>
        public int StartupDelaySeconds { get; set; }

        public bool EnableAutomaticLaneConnections { get; set; }

        /// <summary>
        /// Конструктор со значениями по умолчанию.
        /// </summary>
        public ConfigData()
        {
            EnableStatistics = true;
            EnableBottleneckDetection = true;
            DebugMode = false;
            ScanInterval = 120;
            ScanDepthMode = 1;
            EnableAdaptiveScanning = true;
            MinFpsThreshold = 45;
            EnableGameLogCapture = false;
            StartupDelaySeconds = 90;
            EnableAutomaticLaneConnections = true;
        }
    }
}
