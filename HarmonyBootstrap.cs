using System;
using Harmony;
using TrafficOptimizer.Patches;

namespace TrafficOptimizer.Core
{
    /// <summary>
    /// Отвечает за загрузку и выгрузку Harmony (v1.x).
    /// Патчи применяются вручную — БЕЗ PatchAll и без поиска внутренних traffic API.
    /// 
    /// ВАЖНО: Никаких сравнений MethodInfo через оператор ==.
    /// Используем только object.ReferenceEquals().
    /// operator== на MethodInfo вызывает MissingMethodException: op_Equality
    /// на старом Mono (.NET 3.5) в Cities: Skylines.
    /// </summary>
    public static class HarmonyBootstrap
    {
        private static HarmonyInstance _harmony;

        /// <summary>
        /// Инициализация Harmony.
        /// </summary>
        public static void Initialize()
        {
            if (GameState.HarmonyInitialized)
                return;

            try
            {
                Logger.Info("Initializing Harmony...");

                _harmony = HarmonyInstance.Create(VersionInfo.HarmonyId);

                // DebugOutputPatch — через его собственный ApplyPatch
                ApplyDebugOutputPatch();

                NetworkChangePatch.ApplyPatch(_harmony);

                GameState.HarmonyInitialized = true;

                Logger.Info("Harmony initialized successfully.");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex);
                GameState.HarmonyInitialized = false;
            }
        }

        private static void ApplyDebugOutputPatch()
        {
            try
            {
                Logger.Info("Applying DebugOutputPatch...");

                // Используем готовый метод ApplyPatch из DebugOutputPatch
                // Он уже использует typeof().GetMethod() без AccessTools
                DebugOutputPatch.ApplyPatch(_harmony);
            }
            catch (Exception ex)
            {
                Logger.Warning("DebugOutputPatch failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Снимает все патчи нашего мода.
        /// </summary>
        public static void Shutdown()
        {
            if (!GameState.HarmonyInitialized)
                return;

            try
            {
                Logger.Info("Removing Harmony patches...");

                _harmony.UnpatchAll();

                GameState.HarmonyInitialized = false;

                Logger.Info("Harmony shutdown completed.");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex);
            }
        }
    }
}
