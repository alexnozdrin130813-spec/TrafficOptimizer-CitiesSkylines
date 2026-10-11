using System.Collections.Generic;
using TrafficOptimizer.Models;

namespace TrafficOptimizer.Utils
{
    public static class LaneLoadAnalyzer
    {
        public static void Analyze(List<RoadData> roads)
        {
            if (roads == null)
                return;

            foreach (RoadData road in roads)
            {
                if (road == null)
                    continue;

                if (road.LaneCount <= 0)
                    continue;

                float load =
                    (float)road.VehicleCount /
                    road.LaneCount;

                road.Congestion = load;

                road.IsBottleneck = load >= 0.85f;
            }
        }
    }
}
