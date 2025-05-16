namespace AutoPatrol.Models
{
    /// <summary>
    /// 巡检结果实体类
    /// </summary>
    public class ResultViewModel
    {
        /// <summary>
        /// 产线
        /// </summary>
        public string? Line { get; set; }

        /// <summary>
        /// 线体顺序
        /// </summary>
        public int? Num { get; set; }

        /// <summary>
        /// 设备类型
        /// </summary>
        public string? DeviceType { get; set; }

        /// <summary>
        /// 设备编码
        /// </summary>
        public string? Code { get; set; }

        /// <summary>
        /// 设备IP
        /// </summary>
        public string? Ip { get; set; }

        /// <summary>
        /// 巡检项目
        /// </summary>
        public string? Item { get; set; }

        /// <summary>
        /// 检查结论
        /// </summary>
        public string? Result { get; set; }

        /// <summary>
        /// 检查描述
        /// </summary>
        public string? Describe { get; set; }

        /// <summary>
        /// 提示信息
        /// </summary>
        public string? Message { get; set; }
    }
}
