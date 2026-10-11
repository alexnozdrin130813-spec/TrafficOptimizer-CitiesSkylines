namespace TrafficOptimizer.Models
{
    /// <summary>
    /// Снимок состояния трафика города.
    /// Создается во время анализа.
    /// </summary>
    public class TrafficSnapshot
    {

        /// <summary>
        /// Количество дорог.
        /// </summary>
        public int RoadCount
        {
            get;
            set;
        }


        /// <summary>
        /// Количество транспорта.
        /// </summary>
        public int VehicleCount
        {
            get;
            set;
        }


        /// <summary>
        /// Количество найденных проблемных мест.
        /// </summary>
        public int BottleneckCount
        {
            get;
            set;
        }


        /// <summary>
        /// Средняя скорость транспорта.
        /// </summary>
        public float AverageSpeed
        {
            get;
            set;
        }


        /// <summary>
        /// Общая загруженность дорог.
        /// 0-100%.
        /// </summary>
        public float TrafficDensity
        {
            get;
            set;
        }


        /// <summary>
        /// Время создания снимка.
        /// </summary>
        public ulong Frame
        {
            get;
            set;
        }

    }
}