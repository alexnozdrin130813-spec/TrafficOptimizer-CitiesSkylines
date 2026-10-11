using System;
using System.Collections.Generic;
using ColossalFramework;
using TrafficOptimizer.Core;
using TrafficOptimizer.Models;

namespace TrafficOptimizer.Utils
{
    public static class VehicleAnalyzer
    {
        private static readonly List<VehicleData> _vehicles =
            new List<VehicleData>(16384);

        private static readonly Dictionary<ushort, float> _lastSpeed =
            new Dictionary<ushort, float>();

        private static readonly Dictionary<ushort, int> _stuckCounter =
            new Dictionary<ushort, int>();

        private static bool _cacheValid;

        private static int _vehicleCount;


        /// <summary>
        /// Инициализация анализатора транспорта.
        /// Вызывается один раз при запуске мода.
        /// </summary>
        public static void Initialize()
        {
            ClearCache();

            Logger.Info(
                "VehicleAnalyzer initialized.");
        }


        public static List<VehicleData> ScanVehicles(
            bool forceRefresh = false)
        {
            if (!forceRefresh && _cacheValid)
                return _vehicles;

            _vehicles.Clear();

            VehicleManager manager =
                Singleton<VehicleManager>.instance;

            if (manager == null)
            {
                Logger.Warning(
                    "VehicleManager not found.");

                return _vehicles;
            }

            ushort maxVehicles = GetScanLimit((int)manager.m_vehicles.m_size);

            for (ushort vehicleId = 0;
                 vehicleId < maxVehicles;
                 vehicleId++)
            {
                Vehicle vehicle = manager.m_vehicles.m_buffer[vehicleId];

                if (!IsValidVehicle(ref vehicle))
                    continue;

                VehicleData data =
                    CreateVehicleData(
                        vehicleId,
                        ref vehicle);

                _vehicles.Add(data);
            }

            _vehicleCount = _vehicles.Count;

            _cacheValid = true;

            Logger.Debug(
                "Vehicle scan complete. Vehicles: " + _vehicleCount + "");

            return _vehicles;
        }

        public static int GetVehicleCount()
        {
            if (!_cacheValid)
                ScanVehicles();

            return _vehicleCount;
        }

        private static ushort GetScanLimit(int totalVehicles)
        {
            int mode = Config.Data.ScanDepthMode;
            mode = Math.Max(1, Math.Min(2, mode));

            int effectiveMode = mode;
            if (Config.Data.EnableAdaptiveScanning)
            {
                float fps = GetCurrentFps();
                if (fps > 0f && fps < Config.Data.MinFpsThreshold)
                {
                    effectiveMode = Math.Max(1, mode - 1);
                }
            }

            switch (effectiveMode)
            {
                case 2:
                    return (ushort)Math.Min(totalVehicles, 25000);
                default:
                    return (ushort)Math.Min(totalVehicles, 10000);
            }
        }

        private static float GetCurrentFps()
        {
            try
            {
                return SimulationManager.instance != null && SimulationManager.instance.m_simulationTimeDelta > 0f
                    ? 1f / SimulationManager.instance.m_simulationTimeDelta
                    : 0f;
            }
            catch
            {
                return 0f;
            }
        }

        private static bool IsValidVehicle(
            ref Vehicle vehicle)
        {
            return (vehicle.m_flags &
                Vehicle.Flags.Created) != 0;
        }

        private static VehicleData CreateVehicleData(
            ushort vehicleId,
            ref Vehicle vehicle)
        {
            VehicleData data =
                new VehicleData();

            data.VehicleId = vehicleId;

            data.CurrentSegment =
                ResolveCurrentSegment(ref vehicle);

            // Исправлено: читаем реальную скорость из данных машины
            // В Vehicle.Frame.m_speed хранится текущая скорость
            // Или можно использовать GetLastFrameVelocity()
            data.Speed = ReadVehicleSpeed(ref vehicle);

            data.IsStuck =
                CalculateStuck(
                    vehicleId,
                    data.Speed);

            data.Type =
                GetVehicleType(vehicle.Info);

            return data;
        }

        /// <summary>
        /// Читает реальную скорость машины из игровых данных.
        /// </summary>
        private static float ReadVehicleSpeed(ref Vehicle vehicle)
        {
            try
            {
                // Способ 1: через GetLastFrameVelocity (возвращает Vector3)
                UnityEngine.Vector3 velocity = vehicle.GetLastFrameVelocity();
                float speed = velocity.magnitude;
                if (speed > 0.001f)
                {
                    // magnitude — в игровых единицах/кадр, переводим в ~км/ч
                    return speed * 0.01f;
                }

                // Способ 2: через m_flags
                // Если машина стоит — скорость 0
                if ((vehicle.m_flags & Vehicle.Flags.Stopped) != 0)
                {
                    return 0f;
                }

                // Способ 3: через скорость из последнего кадра
                if (speed > 0f)
                {
                    return speed * 0.01f;
                }

                return 0f;
            }
            catch
            {
                return 0f;
            }
        }

        /// <summary>
        /// Определяет текущий сегмент дороги, на котором находится машина.
        /// Идёт через путь машины (PathUnit), т.к. у Vehicle нет прямой
        /// ссылки на сегмент, а у NetSegment нет списка машин напрямую.
        /// </summary>
        private static ushort ResolveCurrentSegment(
            ref Vehicle vehicle)
        {
            if (vehicle.m_path == 0)
                return 0;

            PathManager pathManager =
                Singleton<PathManager>.instance;

            byte pathPositionIndex =
                vehicle.m_pathPositionIndex;

            if (pathPositionIndex == 255)
                pathPositionIndex = 0;

            PathUnit.Position position;

            bool found =
                pathManager.m_pathUnits.m_buffer[vehicle.m_path]
                    .GetPosition(
                        pathPositionIndex >> 1,
                        out position);

            if (!found)
                return 0;

            return position.m_segment;
        }

        private static VehicleType GetVehicleType(
            VehicleInfo info)
        {
            if (info == null)
                return VehicleType.Unknown;

            switch (info.m_class.m_service)
            {
                case ItemClass.Service.PublicTransport:
                    return VehicleType.PublicTransport;

                case ItemClass.Service.Garbage:

                case ItemClass.Service.FireDepartment:

                case ItemClass.Service.PoliceDepartment:

                case ItemClass.Service.HealthCare:
                    return VehicleType.Emergency;

                case ItemClass.Service.Industrial:

                case ItemClass.Service.PlayerIndustry:
                    return VehicleType.Cargo;

                case ItemClass.Service.Residential:
                    return VehicleType.CitizenCar;

                default:
                    return VehicleType.Service;
            }
        }

        private static bool CalculateStuck(
            ushort vehicleId,
            float speed)
        {
            if (!_lastSpeed.ContainsKey(vehicleId))
            {
                _lastSpeed.Add(vehicleId, speed);
                _stuckCounter.Add(vehicleId, 0);

                return false;
            }

            if (speed < 0.2f)
            {
                _stuckCounter[vehicleId]++;
            }
            else
            {
                _stuckCounter[vehicleId] = 0;
            }

            _lastSpeed[vehicleId] = speed;

            return _stuckCounter[vehicleId] > 30;
        }

        public static void Refresh()
        {
            _cacheValid = false;
            ScanVehicles(true);
        }

        public static void ClearCache()
        {
            _vehicles.Clear();
            _lastSpeed.Clear();
            _stuckCounter.Clear();

            _vehicleCount = 0;

            _cacheValid = false;
        }

        public static int CountMovingVehicles()
        {
            if (!_cacheValid)
                ScanVehicles();

            int count = 0;

            foreach (VehicleData vehicle in _vehicles)
            {
                if (!vehicle.IsStuck)
                    count++;
            }

            return count;
        }


        /// <summary>
        /// Остановка анализатора транспорта.
        /// Вызывается при выгрузке мода.
        /// </summary>
        public static void Shutdown()
        {
            ClearCache();

            Logger.Info(
                "VehicleAnalyzer shutdown.");
        }
    }
}
