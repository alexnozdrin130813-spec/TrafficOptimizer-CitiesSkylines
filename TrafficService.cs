using System;
using TrafficOptimizer.Core;
using TrafficOptimizer.Models;
using TrafficOptimizer.Utils;

namespace TrafficOptimizer.Services
{
    /// <summary>
    /// Основной сервис управления трафиком.
    /// Отвечает за анализ дорожной ситуации.
    /// </summary>
    public sealed class TrafficService : ServiceBase
    {

        public override string Name { get { return "Traffic Service"; } }


        private ulong _lastAnalysisFrame;

        private TrafficSnapshot _currentSnapshot;



        protected override void OnInitialize()
        {
            Logger.Info(
                "TrafficService initialized.");

            _currentSnapshot = new TrafficSnapshot();

            _lastAnalysisFrame = 0;
        }



        /// <summary>
        /// Основной цикл анализа.
        /// </summary>
        protected override void OnUpdate()
        {
            if (!GameState.CityLoaded)
                return;

            if ((DateTime.Now - GameState.CityLoadedTime).TotalSeconds < Config.Data.StartupDelaySeconds)
                return;

            if (!Config.Data.EnableStatistics && !Config.Data.EnableBottleneckDetection)
                return;

            ulong analysisInterval = (ulong)GetAnalysisIntervalFrames();
            if (_lastAnalysisFrame != 0 &&
                GameState.SimulationFrame - _lastAnalysisFrame < analysisInterval)
                return;

            _lastAnalysisFrame = GameState.SimulationFrame;
            AnalyzeTraffic();
        }



        /// <summary>
        /// Анализ состояния трафика.
        /// </summary>
        private void AnalyzeTraffic()
        {
            var roads = RoadAnalyzer.ScanRoads(true);

            LaneLoadAnalyzer.Analyze(roads);


            _currentSnapshot.RoadCount = roads.Count;

            _currentSnapshot.VehicleCount =
                VehicleAnalyzer.GetVehicleCount();

            _currentSnapshot.BottleneckCount =
                RoadAnalyzer.GetBottleneckCount();

            _currentSnapshot.TrafficDensity =
                RoadAnalyzer.GetAverageCongestion() * 100f;

            _currentSnapshot.Frame =
                GameState.SimulationFrame;


            Logger.Debug(
                "Traffic scan: Roads=" + _currentSnapshot.RoadCount + ", Vehicles=" + _currentSnapshot.VehicleCount + "");


            GameState.MarkTrafficScan();


            DecisionEngine decisionEngine =
                ServiceLocator.Get<DecisionEngine>();

            if (decisionEngine != null)
            {
                decisionEngine.UpdateSnapshot(_currentSnapshot);
            }
        }



        /// <summary>
        /// Остановка сервиса.
        /// </summary>
        protected override void OnShutdown()
        {
            Logger.Info(
                "TrafficService stopped.");
        }

        private int GetAnalysisIntervalFrames()
        {
            int intervalSeconds = Math.Max(120, Math.Min(600, Config.Data.ScanInterval));
            return intervalSeconds * 120;
        }
    }
}
