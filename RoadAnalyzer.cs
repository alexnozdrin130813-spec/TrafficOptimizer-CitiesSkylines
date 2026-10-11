using System;
using System.Collections.Generic;
using ColossalFramework;
using TrafficOptimizer.Core;
using TrafficOptimizer.Models;

namespace TrafficOptimizer.Utils
{
    /// <summary>
    /// Анализатор дорожной сети.
    /// Получает реальные данные из NetManager.
    /// </summary>
    public static class RoadAnalyzer
    {
        private static readonly List<RoadData> _roads =
            new List<RoadData>(4096);

        private static readonly HashSet<ushort> _prioritySegments =
            new HashSet<ushort>();

        private static bool _cacheValid;
        private static bool _prioritySegmentsValid;

        private static int _cachedRoadCount;


        /// <summary>
        /// Инициализация анализатора дорог.
        /// Вызывается один раз при запуске мода.
        /// </summary>
        public static void Initialize()
        {
            ClearCache();

            Logger.Info(
                "RoadAnalyzer initialized.");
        }


        public static List<RoadData> ScanRoads(bool forceRefresh = false)
        {
            if (!forceRefresh && _cacheValid)
                return _roads;

            _roads.Clear();

            NetManager manager = Singleton<NetManager>.instance;

            if (manager == null)
            {
                Logger.Warning("NetManager not found.");
                return _roads;
            }

            Dictionary<ushort, int> vehiclesPerSegment =
                BuildVehicleCountPerSegment(forceRefresh);

            HashSet<ushort> relevantSegments = GetPrioritySegments();

            ushort maxSegments = (ushort)manager.m_segments.m_size;

            for (ushort segmentId = 0;
                 segmentId < maxSegments;
                 segmentId++)
            {
                if (relevantSegments.Count > 0 && !relevantSegments.Contains(segmentId))
                    continue;

                NetSegment segment = manager.m_segments.m_buffer[segmentId];

                if (!IsValidSegment(ref segment))
                    continue;

                RoadData road = CreateRoadData(
                    segmentId,
                    ref segment,
                    vehiclesPerSegment);

                _roads.Add(road);
            }

            _cachedRoadCount = _roads.Count;
            _cacheValid = true;

            Logger.Debug(
                "Road scan finished. Segments: " + _cachedRoadCount);

            return _roads;
        }

        /// <summary>
        /// Считает количество машин на каждом сегменте.
        /// </summary>
        private static Dictionary<ushort, int> BuildVehicleCountPerSegment(
            bool forceRefresh)
        {
            List<VehicleData> vehicles =
                VehicleAnalyzer.ScanVehicles(forceRefresh);

            Dictionary<ushort, int> counts =
                new Dictionary<ushort, int>();

            foreach (VehicleData vehicle in vehicles)
            {
                if (vehicle.CurrentSegment == 0)
                    continue;

                int current;
                if (counts.TryGetValue(
                        vehicle.CurrentSegment,
                        out current))
                {
                    counts[vehicle.CurrentSegment] =
                        current + 1;
                }
                else
                {
                    counts[vehicle.CurrentSegment] = 1;
                }
            }

            return counts;
        }

        public static void InvalidateCache()
        {
            _cacheValid = false;
            _prioritySegmentsValid = false;
        }

        public static int GetRoadCount()
        {
            if (!_cacheValid)
                ScanRoads();

            return _cachedRoadCount;
        }

        public static RoadData GetRoad(ushort segmentId)
        {
            if (!_cacheValid)
                ScanRoads();

            for (int i = 0; i < _roads.Count; i++)
            {
                if (_roads[i].SegmentId == segmentId)
                    return _roads[i];
            }

            return null;
        }

        private static bool IsValidSegment(
            ref NetSegment segment)
        {
            if ((segment.m_flags &
                NetSegment.Flags.Created) == 0)
            {
                return false;
            }

            if (segment.Info == null)
                return false;

            return true;
        }

        private static RoadData CreateRoadData(
            ushort segmentId,
            ref NetSegment segment,
            Dictionary<ushort, int> vehiclesPerSegment)
        {
            RoadData road = new RoadData
            {
                SegmentId = segmentId,
                LaneCount = CountVehicleLanes(segment.Info),
                VehicleCount = 0,
                AverageSpeed = 0f,
                Congestion = 0f,
                IsBottleneck = false
            };

            try
            {
                int vehicleCount;
                vehiclesPerSegment.TryGetValue(
                    segmentId,
                    out vehicleCount);

                road.VehicleCount = vehicleCount;

                road.Congestion =
                    CalculateCongestion(
                        road.VehicleCount,
                        road.LaneCount);
            }
            catch (Exception ex)
            {
                Logger.Error(
                    "RoadAnalyzer: segment " + segmentId + ": " + ex.ToString());
            }

            return road;
        }

        private static int CountVehicleLanes(
            NetInfo info)
        {
            if (info == null)
                return 0;

            if (info.m_lanes == null)
                return 0;

            int lanes = 0;

            foreach (NetInfo.Lane lane in info.m_lanes)
            {
                if ((lane.m_laneType &
                    NetInfo.LaneType.Vehicle) != 0)
                {
                    lanes++;
                }
            }

            return lanes;
        }

        private static float CalculateCongestion(
            int vehicles,
            int lanes)
        {
            if (lanes <= 0)
                return 0f;

            float density =
                (float)vehicles / lanes;

            if (density > 1f)
                density = 1f;

            if (density < 0f)
                density = 0f;

            return density;
        }

        public static void Refresh()
        {
            InvalidateCache();

            ScanRoads(true);
        }

        public static float GetAverageLaneCount()
        {
            if (!_cacheValid)
                ScanRoads();

            if (_roads.Count == 0)
                return 0f;

            int total = 0;

            foreach (RoadData road in _roads)
            {
                total += road.LaneCount;
            }

            return (float)total / _roads.Count;
        }

        public static int GetTotalVehicleCount()
        {
            if (!_cacheValid)
                ScanRoads();

            int total = 0;

            foreach (RoadData road in _roads)
            {
                total += road.VehicleCount;
            }

            return total;
        }

        public static void ClearCache()
        {
            _roads.Clear();
            _prioritySegments.Clear();

            _cachedRoadCount = 0;

            _cacheValid = false;
            _prioritySegmentsValid = false;
        }

        public static bool IsCacheValid
        {
            get
            {
                return _cacheValid;
            }
        }

        public static List<RoadData> Roads
        {
            get
            {
                if (!_cacheValid)
                    ScanRoads();

                return _roads;
            }
        }

        public static HashSet<ushort> GetPrioritySegments()
        {
            if (_prioritySegmentsValid)
                return _prioritySegments;

            _prioritySegments.Clear();

            NetManager manager = Singleton<NetManager>.instance;
            if (manager == null)
            {
                _prioritySegmentsValid = true;
                return _prioritySegments;
            }

            ushort maxNodes = (ushort)manager.m_nodes.m_size;
            for (ushort nodeId = 0; nodeId < maxNodes; nodeId++)
            {
                NetNode node = manager.m_nodes.m_buffer[nodeId];
                if ((node.m_flags & NetNode.Flags.Created) == 0)
                    continue;

                int connectedSegments = 0;
                if (node.m_segment0 != 0) connectedSegments++;
                if (node.m_segment1 != 0) connectedSegments++;
                if (node.m_segment2 != 0) connectedSegments++;
                if (node.m_segment3 != 0) connectedSegments++;

                if (connectedSegments < 3)
                    continue;

                AddSegmentIfValid(node.m_segment0, _prioritySegments);
                AddSegmentIfValid(node.m_segment1, _prioritySegments);
                AddSegmentIfValid(node.m_segment2, _prioritySegments);
                AddSegmentIfValid(node.m_segment3, _prioritySegments);
            }

            if (_prioritySegments.Count == 0)
            {
                ushort maxSegments = (ushort)manager.m_segments.m_size;
                for (ushort segmentId = 0; segmentId < maxSegments; segmentId++)
                {
                    NetSegment segment = manager.m_segments.m_buffer[segmentId];
                    if (IsValidSegment(ref segment))
                        _prioritySegments.Add(segmentId);
                }
            }

            _prioritySegmentsValid = true;
            return _prioritySegments;
        }

        private static void AddSegmentIfValid(ushort segmentId, HashSet<ushort> output)
        {
            if (segmentId == 0)
                return;

            NetManager manager = Singleton<NetManager>.instance;
            if (manager == null || segmentId >= manager.m_segments.m_size)
                return;

            NetSegment segment = manager.m_segments.m_buffer[segmentId];
            if (IsValidSegment(ref segment))
                output.Add(segmentId);
        }

        public static float GetAverageCongestion()
        {
            if (!_cacheValid)
                ScanRoads();

            if (_roads.Count == 0)
                return 0f;

            float total = 0f;

            foreach (RoadData road in _roads)
            {
                total += road.Congestion;
            }

            return total / _roads.Count;
        }

        public static int GetBottleneckCount()
        {
            if (!_cacheValid)
                ScanRoads();

            int count = 0;

            foreach (RoadData road in _roads)
            {
                if (road.IsBottleneck)
                    count++;
            }

            return count;
        }


        /// <summary>
        /// Остановка анализатора дорог.
        /// Вызывается при выгрузке мода.
        /// </summary>
        public static void Shutdown()
        {
            ClearCache();

            Logger.Info(
                "RoadAnalyzer shutdown.");
        }
    }
}
