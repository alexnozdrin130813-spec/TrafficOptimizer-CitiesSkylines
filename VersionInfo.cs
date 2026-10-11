using System.Reflection;

namespace TrafficOptimizer.Core
{
    /// <summary>
    /// Информация о версии TrafficOptimizer.
    /// </summary>
    public static class VersionInfo
    {
        /// <summary>
        /// Название мода.
        /// </summary>
        public const string ModName = "TrafficOptimizer";

        /// <summary>
        /// Текущая версия.
        /// </summary>
        public const string Version = "1.3.3 Beta";

        /// <summary>
        /// Автор.
        /// </summary>
        public const string Author = "Alexey Nozdrin";

        /// <summary>
        /// Harmony ID.
        /// </summary>
        public const string HarmonyId = "com.trafficoptimizer.mod";

        /// <summary>
        /// Полное название.
        /// </summary>
        public static string FullName
        {
            get
            {
                return ModName + " v" + Version;
            }
        }

        /// <summary>
        /// Версия сборки.
        /// </summary>
        public static string AssemblyVersion
        {
            get
            {
                try
                {
                    System.Version v = Assembly.GetExecutingAssembly().GetName().Version;
                    if (v != null)
                    {
                        return v.ToString();
                    }
                }
                catch
                {
                    // Игнорируем
                }
                return Version;
            }
        }
    }
}
