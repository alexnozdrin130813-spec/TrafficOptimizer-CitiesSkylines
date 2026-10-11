using ICities;
using System;
using System.IO;
using System.Text;
using ColossalFramework.Plugins;

namespace TrafficOptimizer.Core
{
    /// <summary>
    /// Центральная система логирования TrafficOptimizer.
    /// Все сообщения мода проходят только через этот класс.
    /// Исправленная версия: добавлен fallback-логгер в файл игры
    /// и защита от NullReferenceException.
    /// </summary>
    public static class Logger
    {
        private static readonly object _lock = new object();

        private static string _logDirectory;
        private static string _logFile;
        private static bool _fileLoggingAvailable;
        private static bool _initialized;

        /// <summary>
        /// Инициализация логгера.
        /// Вызывать один раз при загрузке мода.
        /// </summary>
        public static void Initialize()
        {
            if (_initialized)
                return;

            try
            {
                string localAppData =
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

                if (!string.IsNullOrEmpty(localAppData))
                {
                    // Старая Mono не поддерживает Path.Combine с 3+ параметрами
                    string colossalOrder = Path.Combine(localAppData, "Colossal Order");
                    string citiesSkylines = Path.Combine(colossalOrder, "Cities_Skylines");
                    string trafficOptimizerDir = Path.Combine(citiesSkylines, "TrafficOptimizer");
                    _logDirectory = Path.Combine(trafficOptimizerDir, "Logs");

                    Directory.CreateDirectory(_logDirectory);

                    // Очищаем старые логи (>1 дня — логи не нужны дольше)
                    CleanupOldLogs(1);

                    _logFile = Path.Combine(_logDirectory, DateTime.Now.ToString("yyyy-MM-dd") + ".log");

                    _fileLoggingAvailable = true;

                    WriteInternal(
                        "SYSTEM",
                        "========================================");

                    WriteInternal(
                        "SYSTEM",
                        "TrafficOptimizer Logger initialized");

                    WriteInternal(
                        "SYSTEM",
                        "Version: " + VersionInfo.Version);

                    WriteInternal(
                        "SYSTEM",
                        "========================================");
                }

                _initialized = true;

                // Пробуем записать в панель отладки игры
                try
                {
                    DebugOutputPanel.AddMessage(
                        PluginManager.MessageType.Message,
                        "[TrafficOptimizer] Logger initialized successfully");
                }
                catch
                {
                    // DebugOutputPanel может быть недоступен на раннем этапе загрузки
                    // Используем FileFallback в таких случаях
                    WriteInternal("SYSTEM", "DebugOutputPanel not available, using file fallback");
                }
            }
            catch (Exception ex)
            {
                // Экстремальный fallback — пишем во временный файл
                try
                {
                    string fallbackLog = Path.Combine(Path.GetTempPath(), "TrafficOptimizer_Logger_Error.log");

                    File.AppendAllText(
                        fallbackLog,
                        string.Format("[{0:HH:mm:ss}] [FATAL] Logger init failed: {1}{2}", DateTime.Now, ex, Environment.NewLine),
                        Encoding.UTF8);
                }
                catch
                {
                    // Если даже это не работает — игнорируем
                }
            }
        }

        /// <summary>
        /// Удаляет логи старше указанного количества дней.
        /// </summary>
        private static void CleanupOldLogs(int maxAgeDays)
        {
            try
            {
                if (string.IsNullOrEmpty(_logDirectory))
                    return;

                if (!Directory.Exists(_logDirectory))
                    return;

                string[] files = Directory.GetFiles(_logDirectory, "*.log");

                DateTime cutoff = DateTime.Now.AddDays(-maxAgeDays);

                for (int i = 0; i < files.Length; i++)
                {
                    try
                    {
                        FileInfo fi = new FileInfo(files[i]);
                        if (fi.Exists && fi.CreationTime < cutoff)
                        {
                            fi.Delete();
                        }
                    }
                    catch
                    {
                        // Игнорируем ошибки удаления отдельных файлов
                    }
                }
            }
            catch
            {
                // Игнорируем ошибки очистки
            }
        }

        /// <summary>
        /// Информационное сообщение.
        /// </summary>
        public static void Info(string message)
        {
            Write("INFO", message);
        }

        /// <summary>
        /// Предупреждение.
        /// </summary>
        public static void Warning(string message)
        {
            Write("WARNING", message);
        }

        /// <summary>
        /// Сообщение об ошибке.
        /// </summary>
        public static void Error(string message)
        {
            Write("ERROR", message);
        }

        /// <summary>
        /// Запись исключения.
        /// </summary>
        public static void Exception(Exception ex)
        {
            if (ex == null)
                return;

            Write("EXCEPTION", ex.ToString());
        }

        /// <summary>
        /// Debug сообщение.
        /// Записывается, когда включён DebugMode, в том числе в Release-сборке.
        /// </summary>
        public static void Debug(string message)
        {
            if (GameState.DebugMode)
                Write("DEBUG", message);
        }

        /// <summary>
        /// Основной метод записи.
        /// </summary>
        private static void Write(string level, string message)
        {
            try
            {
                WriteInternal(level, message);

                if (string.Equals(level, "DEBUG", StringComparison.OrdinalIgnoreCase))
                    return;

                if (string.Equals(level, "ERROR", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(level, "EXCEPTION", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        UnityEngine.Debug.LogError("[TrafficOptimizer] [" + level + "] " + message);
                    }
                    catch
                    {
                        // The game log may not be available during early startup.
                    }
                }

                // Безопасная запись в панель отладки игры
                try
                {
                    PluginManager.MessageType type =
                        PluginManager.MessageType.Message;

                    switch (level)
                    {
                        case "WARNING":
                            type = PluginManager.MessageType.Warning;
                            break;

                        case "ERROR":
                        case "EXCEPTION":
                            type = PluginManager.MessageType.Error;
                            break;
                    }

                    DebugOutputPanel.AddMessage(
                        type,
                        "[TrafficOptimizer] [" + level + "] " + message);
                }
                catch
                {
                    // DebugOutputPanel не доступен — пишем только в файл
                }
            }
            catch
            {
                // Никогда не допускаем падения игры из-за логгера.
            }
        }

        /// <summary>
        /// Записывает сообщение в лог-файл.
        /// </summary>
        private static void WriteInternal(string level, string message)
        {
            if (!_fileLoggingAvailable || string.IsNullOrEmpty(_logFile))
                return;

            try
            {
                string line = string.Format(
                    "[{0:HH:mm:ss}] [{1}] {2}",
                    DateTime.Now,
                    level,
                    message);

                File.AppendAllText(
                    _logFile,
                    line + Environment.NewLine,
                    Encoding.UTF8);
            }
            catch
            {
                // Ошибка записи в файл — не критично
            }
        }

        /// <summary>
        /// Очистка ресурсов.
        /// </summary>
        public static void Shutdown()
        {
            try
            {
                WriteInternal(
                    "SYSTEM",
                    "TrafficOptimizer shutdown.");

                WriteInternal(
                    "SYSTEM",
                    "========================================");
            }
            catch
            {
                // Игнорируем любые ошибки при завершении.
            }

            _initialized = false;
        }
    }
}
