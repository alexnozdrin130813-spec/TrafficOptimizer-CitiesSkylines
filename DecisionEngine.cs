using TrafficOptimizer.Core;
using TrafficOptimizer.Models;

namespace TrafficOptimizer.Services
{
	/// <summary>
	/// Центральная система принятия решений.
	/// Анализирует данные и выбирает действия.
	/// </summary>
	public sealed class DecisionEngine : ServiceBase
	{

		public override string Name { get { return "Decision Engine"; } }



		private TrafficSnapshot _snapshot;



		protected override void OnInitialize()
		{
			Logger.Info(
				"DecisionEngine initialized.");


			_snapshot =
				new TrafficSnapshot();
		}



		protected override void OnUpdate()
		{
			/*
             * Пока ждём данные от TrafficService.
             *
             * Позже:
             *
             * анализ пробок
             * анализ полос
             * расчёт решений
             */
		}



		/// <summary>
		/// Передача нового состояния города.
		/// </summary>
		public void UpdateSnapshot(
			TrafficSnapshot snapshot)
		{
			if (snapshot == null)
				return;


			_snapshot = snapshot;


			Analyze();
		}



		/// <summary>
		/// Анализ состояния.
		/// </summary>
		private void Analyze()
		{

			if (_snapshot.TrafficDensity > 80)
			{
				Logger.Debug(
					"High traffic density detected.");
			}


			if (_snapshot.BottleneckCount > 0)
			{
				Logger.Debug(
					"Traffic bottlenecks detected.");
			}

		}



		protected override void OnShutdown()
		{
			_snapshot = null;


			Logger.Info(
				"DecisionEngine stopped.");
		}

	}
}