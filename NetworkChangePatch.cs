using System;
using System.Reflection;
using Harmony;
using TrafficOptimizer.Core;

namespace TrafficOptimizer.Patches
{
    public static class NetworkChangePatch
    {
        private static int _revision;

        public static int Revision
        {
            get { return _revision; }
        }

        public static void ApplyPatch(HarmonyInstance harmony)
        {
            MethodInfo createPostfix = typeof(NetworkChangePatch).GetMethod(
                "AfterCreateSegment", BindingFlags.Public | BindingFlags.Static);
            MethodInfo releasePostfix = typeof(NetworkChangePatch).GetMethod(
                "AfterReleaseSegment", BindingFlags.Public | BindingFlags.Static);

            MethodInfo[] methods = typeof(NetManager).GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            int patchedCreate = 0;
            int patchedRelease = 0;
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                try
                {
                    if (method.Name == "CreateSegment" &&
                        object.ReferenceEquals(method.ReturnType, typeof(bool)) &&
                        method.GetParameters().Length >= 10)
                    {
                        harmony.Patch(method, null, new HarmonyMethod(createPostfix));
                        patchedCreate++;
                    }
                    else if (method.Name == "ReleaseSegment" &&
                             object.ReferenceEquals(method.ReturnType, typeof(void)) &&
                             method.GetParameters().Length > 0)
                    {
                        harmony.Patch(method, null, new HarmonyMethod(releasePostfix));
                        patchedRelease++;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warning("Network change patch failed for " + method.Name + ": " + ex.Message);
                }
            }

            Logger.Info("Network change patches applied. Create=" + patchedCreate + ", release=" + patchedRelease);
        }

        public static void AfterCreateSegment(bool __result)
        {
            if (__result && GameState.CityLoaded)
                unchecked { _revision++; }
        }

        public static void AfterReleaseSegment()
        {
            if (GameState.CityLoaded)
                unchecked { _revision++; }
        }
    }
}