namespace TrafficOptimizer.Models
{
    /// <summary>
    /// Данные дорожного сегмента.
    /// </summary>
    public class RoadData
    {

        /// <summary>
        /// ID сегмента в игре.
        /// </summary>
        public ushort SegmentId
        {
            get;
            set;
        }


        /// <summary>
        /// Количество полос.
        /// </summary>
        public int LaneCount
        {
            get;
            set;
        }


        /// <summary>
        /// Количество машин на участке.
        /// </summary>
        public int VehicleCount
        {
            get;
            set;
        }


        /// <summary>
        /// Средняя скорость.
        /// </summary>
        public float AverageSpeed
        {
            get;
            set;
        }


        /// <summary>
        /// Уровень загрузки дороги 0-100.
        /// </summary>
        public float Congestion
        {
            get;
            set;
        }


        /// <summary>
        /// Является ли участок проблемным.
        /// </summary>
        public bool IsBottleneck
        {
            get;
            set;
        }

    }
}