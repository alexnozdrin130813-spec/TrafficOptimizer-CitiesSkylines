using System.Collections.Generic;
using TrafficOptimizer.Models;

namespace TrafficOptimizer.Utils
{
    public static class BottleneckAnalyzer
    {
        public static List<RoadData> FindBottlenecks(
            List<RoadData> roads)
        {
            List<RoadData> result =
                new List<RoadData>();

            if (roads == null)
                return result;

            foreach (RoadData road in roads)
            {
                if (road == null)
                    continue;

                if (road.LaneCount <= 0)
                    continue;

                float density =
                    (float)road.VehicleCount /
                    road.LaneCount;

                if (density >= 6f)
                {
                    road.IsBottleneck = true;
                    result.Add(road);
                }
                else
                {
                    road.IsBottleneck = false;
                }
            }

            return result;
        }
    }
}
