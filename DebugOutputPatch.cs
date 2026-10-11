using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using ColossalFramework.Plugins;
using Harmony;
using TrafficOptimizer.Core;

namespace TrafficOptimizer.Patches
{
    /// <summary>
    /// Патч на DebugOutputPanel.AddMessage (Harmony v1.x).
    /// Перехватывает сообщения из игрового окна лога и сохраняет
    /// только те, что относятся к TrafficOptimizer, а также все ошибки (Error).
    /// 
    /// ВАЖНО: Используем typeof().GetMethod() + object.ReferenceEquals.
    /// НЕ ИСПОЛЬЗУЕМ AccessTools.Method или == для MethodInfo.
    /// 
    /// Проблема: точная сигнатура DebugOutputPanel.AddMessage неизвестна.
    /// Поэтому ищем метод по ИМЕНИ, перебирая все методы класса.
    /// </summary>
    public static class DebugOutputPatch
    {
        private static string _logFile;
        private static bool _initialized;
        private static readonly object _pendingLogLock = new object();
        private static readonly Queue<string> _pendingLogLines = new Queue<string>();
        private const int MaxPendingLogLines = 512;
        private static bool _logWriterScheduled;

        /// <summary>
        /// Применяет патч вручную (Harmony v1.x API).
        /// Ищет AddMessage по имени, а не по сигнатуре.
        /// </summary>
        public static void ApplyPatch(HarmonyInstance harmony)
        {
            try
            {
                // Ищем метод AddMessage перебором всех статических методов DebugOutputPanel
                MethodInfo original = FindAddMessageMethod();

                if (object.ReferenceEquals(original, null))
                {
                    Logger.Warning("DebugOutputPatch: DebugOutputPanel.AddMessage not found.");
                    return;
                }

                var prefix = typeof(DebugOutputPatch).GetMethod(
                    "BeforeAddMessage",
                    BindingFlags.Public | BindingFlags.Static);

                if (object.ReferenceEquals(prefix, null))
                {
                    Logger.Warning("DebugOutputPatch: BeforeAddMessage not found (internal error).");
                    return;
                }

                harmony.Patch(original, new HarmonyMethod(prefix), null);
                Logger.Info("DebugOutputPatch: Applied successfully to " + original.Name);
            }
            catch (Exception ex)
            {
                Logger.Warning("DebugOutputPatch: Failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Ищет метод AddMessage в DebugOutputPanel перебором.
        /// </summary>
        private static MethodInfo FindAddMessageMethod()
        {
            try
            {
                MethodInfo[] methods = typeof(DebugOutputPanel).GetMethods(
                    BindingFlags.Public | BindingFlags.Static);

                for (int i = 0; i < methods.Length; i++)
                {
                    if (methods[i].Name == "AddMessage")
                    {
                        return methods[i];
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        private static void EnsureInitialized()
        {
            if (_initialized)
                return;

            try
            {
                string localAppData =
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

                if (!string.IsNullOrEmpty(localAppData))
                {
                    string logDir = Path.Combine(
                        localAppData, "Colossal Order", "Cities_Skylines",
                        "TrafficOptimizer", "Logs");

                    Directory.CreateDirectory(logDir);

                    // Удаляем логи за предыдущие дни (оставляем только сегодняшний)
                    CleanOldLogFiles(logDir);

                    string today = DateTime.Now.ToString("yyyy-MM-dd");
                    _logFile = Path.Combine(logDir, "GameLog_" + today + ".log");

                    _initialized = true;
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// Удаляет лог-файлы за предыдущие дни, оставляя только сегодняшний.
        /// </summary>
        private static void CleanOldLogFiles(string logDir)
        {
            try
            {
                if (!Directory.Exists(logDir))
                    return;

                string todayPrefix = "GameLog_" + DateTime.Now.ToString("yyyy-MM-dd");

                string[] files = Directory.GetFiles(logDir, "GameLog_*.log");
                for (int i = 0; i < files.Length; i++)
                {
                    // Если имя файла не содержит сегодняшнюю дату — удаляем
                    if (!files[i].Contains(todayPrefix))
                    {
                        try
                        {
                            File.Delete(files[i]);
                        }
                        catch
                        {
                            // Не можем удалить — игнорируем
                        }
                    }
                }
            }
            catch
            {
                // Игнорируем ошибки очистки
            }
        }

        public static bool BeforeAddMessage(
            PluginManager.MessageType type,
            string message)
        {
            try
            {
                if (!IsCaptureEnabled())
                    return true;

                bool isError = (type == PluginManager.MessageType.Error);
                bool isTrafficOptimizer = !string.IsNullOrEmpty(message) &&
                                          message.Contains("TrafficOptimizer");

                if (!isError && !isTrafficOptimizer)
                    return true;

                WriteToFile(type, message);
            }
            catch
            {
            }

            return true;
        }

        private static bool IsCaptureEnabled()
        {
            try
            {
                return Config.Data.EnableGameLogCapture;
            }
            catch
            {
                return true;
            }
        }

        private static void WriteToFile(
            PluginManager.MessageType type,
            string message)
        {
            EnsureInitialized();

            if (!_initialized || string.IsNullOrEmpty(_logFile))
                return;

            try
            {
                string typeStr = "MESSAGE";
                switch (type)
                {
                    case PluginManager.MessageType.Warning:
                        typeStr = "WARNING";
                        break;
                    case PluginManager.MessageType.Error:
                        typeStr = "ERROR";
                        break;
                }

                string line = string.Format(
                    "[{0:HH:mm:ss}] [{1}] {2}",
                    DateTime.Now,
                    typeStr,
                    message ?? "(null)");

                bool scheduleWriter = false;
                lock (_pendingLogLock)
                {
                    if (_pendingLogLines.Count >= MaxPendingLogLines)
                        _pendingLogLines.Dequeue();

                    _pendingLogLines.Enqueue(line);
                    if (!_logWriterScheduled)
                    {
                        _logWriterScheduled = true;
                        scheduleWriter = true;
                    }
                }

                if (scheduleWriter && !ThreadPool.QueueUserWorkItem(DrainLogQueue))
                {
                    lock (_pendingLogLock)
                    {
                        _logWriterScheduled = false;
                    }
                }
            }
            catch
            {
            }
        }

        private static void DrainLogQueue(object state)
        {
            while (true)
            {
                List<string> batch;
                lock (_pendingLogLock)
                {
                    if (_pendingLogLines.Count == 0)
                    {
                        _logWriterScheduled = false;
                        return;
                    }

                    batch = new List<string>(_pendingLogLines);
                    _pendingLogLines.Clear();
                }

                try
                {
                    EnsureInitialized();
                    if (_initialized && !string.IsNullOrEmpty(_logFile))
                    {
                        File.AppendAllText(
                            _logFile,
                            string.Join(Environment.NewLine, batch.ToArray()) + Environment.NewLine,
                            Encoding.UTF8);
                    }
                }
                catch
                {
                }
            }
        }
    }
}
