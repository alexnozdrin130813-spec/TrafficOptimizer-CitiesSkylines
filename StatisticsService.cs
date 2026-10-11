using TrafficOptimizer.Core;

namespace TrafficOptimizer.Services
{
    /// <summary>
    /// Сервис сбора статистики трафика.
    /// </summary>
    public sealed class StatisticsService : ServiceBase
    {

        public override string Name { get { return "Statistics Service"; } }


        private int _updateCounter;


        /// <summary>
        /// Количество выполненных анализов.
        /// </summary>
        public static int AnalysisCount { get; private set; }


        /// <summary>
        /// Последний кадр анализа.
        /// </summary>
        public static ulong LastAnalysisFrame { get; private set; }



        protected override void OnInitialize()
        {
            Logger.Info(
                "StatisticsService initialized.");


            AnalysisCount = 0;
            LastAnalysisFrame = 0;

            _updateCounter = 0;
        }



        protected override void OnUpdate()
        {
            _updateCounter++;


            /*
             * Статистика не должна считаться каждый кадр.
             * Это будет отдельный лёгкий процесс.
             */


            if (_updateCounter >= 300)
            {
                CollectStatistics();

                _updateCounter = 0;
            }
        }



        /// <summary>
        /// Сбор статистики.
        /// </summary>
        private void CollectStatistics()
        {
            AnalysisCount++;

            LastAnalysisFrame =
                GameState.SimulationFrame;


            Logger.Debug(
                "Statistics update #" + AnalysisCount + "");
        }



        protected override void OnShutdown()
        {
            Logger.Info(
                "StatisticsService stopped.");
        }
    }
}