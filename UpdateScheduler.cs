using System;
using TrafficOptimizer.Core;

namespace TrafficOptimizer.Utils
{
    /// <summary>
    /// Планировщик обновлений для систем TrafficOptimizer.
    /// Позволяет запускать задачи с определённой периодичностью,
    /// чтобы не нагружать игру каждый кадр.
    /// </summary>
    public class UpdateScheduler
    {
        /// <summary>
        /// Делегат для задачи обновления.
        /// </summary>
        public delegate void UpdateTask();

        private readonly string _name;
        private readonly UpdateTask _task;
        private readonly int _interval;
        private int _counter;

        /// <summary>
        /// Создаёт планировщик с указанным интервалом.
        /// </summary>
        /// <param name="name">Имя задачи (для логов)</param>
        /// <param name="task">Делегат задачи</param>
        /// <param name="intervalFrames">Интервал в кадрах (по умолчанию 300 = ~5 секунд)</param>
        public UpdateScheduler(string name, UpdateTask task, int intervalFrames = 300)
        {
            _name = name ?? "Unknown";
            if (task == null)
                throw new ArgumentNullException("task");
            _task = task;
            _interval = Math.Max(1, intervalFrames);
            _counter = 0;
        }

        /// <summary>
        /// Вызывать каждый кадр. Запустит задачу, если наступило время.
        /// </summary>
        public void Tick()
        {
            _counter++;

            if (_counter >= _interval)
            {
                _counter = 0;

                try
                {
                    _task.Invoke();
                }
                catch (Exception ex)
                {
                    Logger.Error("UpdateScheduler [" + _name + "]: " + ex.Message);
                    Logger.Exception(ex);
                }
            }
        }

        /// <summary>
        /// Сбрасывает счётчик.
        /// </summary>
        public void Reset()
        {
            _counter = 0;
        }
    }
}
