namespace TrafficOptimizer.Models
{
    /// <summary>
    /// Данные транспорта в городе.
    /// </summary>
    public class VehicleData
    {

        /// <summary>
        /// ID транспорта.
        /// </summary>
        public ushort VehicleId
        {
            get;
            set;
        }


        /// <summary>
        /// Тип транспорта.
        /// </summary>
        public VehicleType Type
        {
            get;
            set;
        }


        /// <summary>
        /// Текущая скорость.
        /// </summary>
        public float Speed
        {
            get;
            set;
        }


        /// <summary>
        /// Находится ли транспорт в пробке.
        /// </summary>
        public bool IsStuck
        {
            get;
            set;
        }


        /// <summary>
        /// ID дороги, по которой движется машина.
        /// </summary>
        public ushort CurrentSegment
        {
            get;
            set;
        }

    }



    /// <summary>
    /// Тип транспорта.
    /// </summary>
    public enum VehicleType
    {
        Unknown,

        CitizenCar,

        Service,

        Emergency,

        PublicTransport,

        Cargo
    }
}