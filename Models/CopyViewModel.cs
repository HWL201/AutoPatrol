namespace AutoPatrol.Models
{
    public class CopyViewModel
    {
        /// <summary>
        /// 执行时间
        /// </summary>
        // public string Time { get; set; }

        /// <summary>
        /// 执行周期
        /// </summary>
        public int Cycle { get; set; } = 60;

        /// <summary>
        /// 执行任务集
        /// </summary>
        public List<ObjectViewModel> Tasks { get; set; }
    }

    public class ObjectViewModel
    {
        /// <summary>
        /// 产线
        /// </summary>
        public string? Line { get; set; }

        /// <summary>
        /// 设备编码
        /// </summary>
        public string? Code { get; set; }

        /// <summary>
        /// 账号
        /// </summary>
        public string? Account { get; set; }

        /// <summary>
        /// 密码
        /// </summary>
        public string? Password { get; set; }

        /// <summary>
        /// 源文件
        /// </summary>
        public string? SourceFile { get; set; }

        /// <summary>
        /// 目标文件
        /// </summary>
        public string? TargetFile { get; set; }
    }
}
