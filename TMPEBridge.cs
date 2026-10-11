using System;
using System.Reflection;

namespace TrafficOptimizer.Services
{
    internal static class TMPEBridge
    {
        private const string ImplementationsTypeName = "TrafficManager.API.Implementations";
        private const string ManagerFactoryTypeName = "TrafficManager.API.Manager.IManagerFactory";
        private const string LaneConnectionManagerTypeName = "TrafficManager.API.Manager.ILaneConnectionManager";

        public static bool TryGetLaneConnectionApi(
            out object manager,
            out MethodInfo hasNodeConnections,
            out MethodInfo addLaneConnection,
            out MethodInfo removeLaneConnection,
            out object transitionGroup)
        {
            manager = null;
            hasNodeConnections = null;
            addLaneConnection = null;
            removeLaneConnection = null;
            transitionGroup = null;

            try
            {
                Type implementationsType = FindType(ImplementationsTypeName);
                if (object.ReferenceEquals(implementationsType, null))
                    return false;

                PropertyInfo factoryProperty = implementationsType.GetProperty(
                    "ManagerFactory", BindingFlags.Public | BindingFlags.Static);
                if (object.ReferenceEquals(factoryProperty, null))
                    return false;

                object factory = factoryProperty.GetValue(null, null);
                if (factory == null)
                    return false;

                Type factoryType = FindType(ManagerFactoryTypeName);
                Type laneManagerType = FindType(LaneConnectionManagerTypeName);
                if (object.ReferenceEquals(factoryType, null) ||
                    object.ReferenceEquals(laneManagerType, null))
                    return false;

                PropertyInfo laneManagerProperty = factoryType.GetProperty("LaneConnectionManager");
                if (object.ReferenceEquals(laneManagerProperty, null))
                    return false;

                manager = laneManagerProperty.GetValue(factory, null);
                if (manager == null)
                    return false;

                hasNodeConnections = laneManagerType.GetMethod("HasNodeConnections");
                MethodInfo[] methods = manager.GetType().GetMethods(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo method = methods[i];
                    if (method.Name != "AddLaneConnection" && method.Name != "RemoveLaneConnection")
                        continue;

                    ParameterInfo[] parameters = method.GetParameters();
                    if (!object.ReferenceEquals(method.ReturnType, typeof(bool)) || parameters.Length != 4 ||
                        !object.ReferenceEquals(parameters[0].ParameterType, typeof(uint)) ||
                        !object.ReferenceEquals(parameters[1].ParameterType, typeof(uint)) ||
                        !object.ReferenceEquals(parameters[2].ParameterType, typeof(bool)) ||
                        !parameters[3].ParameterType.IsEnum ||
                        parameters[3].ParameterType.Name != "LaneEndTransitionGroup")
                        continue;

                    if (method.Name == "AddLaneConnection")
                    {
                        addLaneConnection = method;
                        transitionGroup = Enum.Parse(parameters[3].ParameterType, "Road");
                    }
                    else
                    {
                        removeLaneConnection = method;
                    }
                }

                  return !object.ReferenceEquals(hasNodeConnections, null) &&
                      !object.ReferenceEquals(addLaneConnection, null) &&
                      !object.ReferenceEquals(removeLaneConnection, null) &&
                      !object.ReferenceEquals(transitionGroup, null);
            }
            catch
            {
                manager = null;
                return false;
            }
        }

        private static Type FindType(string fullName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type type = assemblies[i].GetType(fullName, false);
                    if (!object.ReferenceEquals(type, null))
                    return type;
            }

            return null;
        }
    }
}