namespace AutoPatrol.Models
{
    /// <summary>
    /// 设备模板实体类
    /// </summary>
    public class DeviceViewModel
    {
        /// <summary>
        /// 产线
        /// </summary>
        public string? Line { get; set; }

        /// <summary>
        /// 线体顺序
        /// </summary>
        public int Num { get; set; } = 1;

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
        /// 日志类型
        /// </summary>
        public string? LogType { get; set; }

        /// <summary>
        /// 日志路径
        /// </summary>
        public string? Path { get; set; }

        /// <summary>
        /// 文件层级
        /// </summary>
        public string? Floor { get; set; }

        /// <summary>
        /// 文件后缀
        /// </summary>
        public string? Postfix { get; set; }

        /// <summary>
        /// 账号
        /// </summary>
        public string? Account { get; set; }

        /// <summary>
        /// 密码
        /// </summary>
        public string? Password { get; set; }

        /// <summary>
        /// 驱动名称
        /// </summary>
        public string? DriverName { get; set; }
    }
}