using System;
using System.IO;
using System.Reflection;

namespace TrafficOptimizer.Core
{
    /// <summary>
    /// Загрузка и сохранение конфигурации TrafficOptimizer.
    /// Исправленная версия: теперь реально загружает/сохраняет XML.
    /// </summary>
    public static class Config
    {
        private static string _configFilePath;
        private static ConfigData _data;

        /// <summary>
        /// Текущие настройки мода.
        /// </summary>
        public static ConfigData Data
        {
            get
            {
                if (_data == null)
                    Load();
                return _data;
            }
        }

        /// <summary>
        /// Загрузка конфигурации из файла.
        /// </summary>
        public static void Load()
        {
            try
            {
                string modPath = GetModPath();
                if (string.IsNullOrEmpty(modPath))
                {
                    Logger.Warning("Config: Could not determine mod path, using defaults.");
                    _data = new ConfigData();
                    return;
                }

                _configFilePath = Path.Combine(modPath, "TrafficOptimizerConfig.xml");
                
                if (File.Exists(_configFilePath))
                {
                    try
                    {
                        string xml = File.ReadAllText(_configFilePath);
                        _data = DeserializeFromXml(xml);
                        Logger.Info("Config loaded successfully.");
                    }
catch (Exception ex)
                    {
                        Logger.Warning("Config: Failed to parse config file: " + ex.Message + ". Using defaults.");
                        _data = new ConfigData();
                    }
                }
                else
                {
                    Logger.Info("Config: No config file found, using defaults.");
                    _data = new ConfigData();
                    Save();
                }
            }
            catch (Exception ex)
            {
                Logger.Warning("Config: Load error: " + ex.Message + ". Using defaults.");
                _data = new ConfigData();
            }
        }

        /// <summary>
        /// Сохранение конфигурации в файл.
        /// </summary>
        public static void Save()
        {
            if (_data == null)
                return;

            try
            {
                if (string.IsNullOrEmpty(_configFilePath))
                {
                    string modPath = GetModPath();
                    if (string.IsNullOrEmpty(modPath))
                        return;
                    _configFilePath = Path.Combine(modPath, "TrafficOptimizerConfig.xml");
                }

                string xml = SerializeToXml(_data);
                File.WriteAllText(_configFilePath, xml);
                Logger.Info("Config saved successfully.");
            }
            catch (Exception ex)
            {
                Logger.Warning("Config: Save error: " + ex.Message + "");
            }
        }

        /// <summary>
        /// Получает путь к папке мода.
        /// </summary>
        private static string GetModPath()
        {
            try
            {
                string assemblyLocation = Assembly.GetExecutingAssembly().Location;
                if (!string.IsNullOrEmpty(assemblyLocation))
                {
                    return Path.GetDirectoryName(assemblyLocation);
                }
            }
            catch
            {
                // Игнорируем
            }

            // Fallback: используем LocalApplicationData
            try
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (!string.IsNullOrEmpty(localAppData))
                {
                    // Старая Mono не поддерживает Path.Combine с 3+ параметрами
                    string colossalOrder = Path.Combine(localAppData, "Colossal Order");
                    string citiesSkylines = Path.Combine(colossalOrder, "Cities_Skylines");
                    string path = Path.Combine(citiesSkylines, "TrafficOptimizer");
                    Directory.CreateDirectory(path);
                    return path;
                }
            }
            catch
            {
                // Игнорируем
            }

            return null;
        }

        /// <summary>
        /// Простая XML-сериализация вручную (без зависимостей).
        /// </summary>
        private static string SerializeToXml(ConfigData data)
        {
            return "<?xml version=\"1.0\" encoding=\"utf-8\"?>" + Environment.NewLine +
                   "<TrafficOptimizerConfig>" + Environment.NewLine +
                   "  <EnableStatistics>" + data.EnableStatistics + "</EnableStatistics>" + Environment.NewLine +
                   "  <EnableBottleneckDetection>" + data.EnableBottleneckDetection + "</EnableBottleneckDetection>" + Environment.NewLine +
                   "  <DebugMode>" + data.DebugMode + "</DebugMode>" + Environment.NewLine +
                   "  <ScanInterval>" + data.ScanInterval + "</ScanInterval>" + Environment.NewLine +
                   "  <ScanDepthMode>" + data.ScanDepthMode + "</ScanDepthMode>" + Environment.NewLine +
                   "  <EnableAdaptiveScanning>" + data.EnableAdaptiveScanning + "</EnableAdaptiveScanning>" + Environment.NewLine +
                   "  <MinFpsThreshold>" + data.MinFpsThreshold + "</MinFpsThreshold>" + Environment.NewLine +
                   "  <EnableGameLogCapture>" + data.EnableGameLogCapture + "</EnableGameLogCapture>" + Environment.NewLine +
                   "  <EnableAutomaticLaneConnections>" + data.EnableAutomaticLaneConnections + "</EnableAutomaticLaneConnections>" + Environment.NewLine +
                   "</TrafficOptimizerConfig>";
        }

        /// <summary>
        /// Простая XML-десериализация вручную.
        /// </summary>
        private static ConfigData DeserializeFromXml(string xml)
        {
            var data = new ConfigData();

            if (string.IsNullOrEmpty(xml))
                return data;

            data.EnableStatistics = ParseXmlBool(xml, "EnableStatistics", data.EnableStatistics);
            data.EnableBottleneckDetection = ParseXmlBool(xml, "EnableBottleneckDetection", data.EnableBottleneckDetection);
            data.DebugMode = ParseXmlBool(xml, "DebugMode", data.DebugMode);
            data.ScanInterval = ParseXmlInt(xml, "ScanInterval", data.ScanInterval);
            data.ScanDepthMode = ParseXmlInt(xml, "ScanDepthMode", data.ScanDepthMode);
            data.EnableAdaptiveScanning = ParseXmlBool(xml, "EnableAdaptiveScanning", data.EnableAdaptiveScanning);
            data.MinFpsThreshold = ParseXmlInt(xml, "MinFpsThreshold", data.MinFpsThreshold);
            data.EnableGameLogCapture = ParseXmlBool(xml, "EnableGameLogCapture", data.EnableGameLogCapture);
            data.EnableAutomaticLaneConnections = ParseXmlBool(xml, "EnableAutomaticLaneConnections", data.EnableAutomaticLaneConnections);

            return data;
        }

        private static bool ParseXmlBool(string xml, string tag, bool defaultValue)
        {
            try
            {
                string openTag = "<" + tag + ">";
                string closeTag = "</" + tag + ">";
                int start = xml.IndexOf(openTag);
                if (start < 0) return defaultValue;
                start += openTag.Length;
                int end = xml.IndexOf(closeTag, start);
                if (end < 0) return defaultValue;
                string value = xml.Substring(start, end - start).Trim();
                bool result;
                return bool.TryParse(value, out result) ? result : defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }

        private static float ParseXmlFloat(string xml, string tag, float defaultValue)
        {
            try
            {
                string openTag = "<" + tag + ">";
                string closeTag = "</" + tag + ">";
                int start = xml.IndexOf(openTag);
                if (start < 0) return defaultValue;
                start += openTag.Length;
                int end = xml.IndexOf(closeTag, start);
                if (end < 0) return defaultValue;
                string value = xml.Substring(start, end - start).Trim();
                float result;
                return float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out result)
                    ? result
                    : defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }

        private static int ParseXmlInt(string xml, string tag, int defaultValue)
        {
            try
            {
                string openTag = "<" + tag + ">";
                string closeTag = "</" + tag + ">";
                int start = xml.IndexOf(openTag);
                if (start < 0) return defaultValue;
                start += openTag.Length;
                int end = xml.IndexOf(closeTag, start);
                if (end < 0) return defaultValue;
                string value = xml.Substring(start, end - start).Trim();
                int result;
                return int.TryParse(value, out result) ? result : defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }
    }
}

