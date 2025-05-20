namespace AutoPatrol.Models
{
    /// <summary>
    /// 定时任务模型
    /// </summary>
    public class TimerViewModel
    {
        /// <summary>
        /// 定时任务名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 时间  HH:MM:SS
        /// </summary>
        public string Time{ get; set; }
    }
}
