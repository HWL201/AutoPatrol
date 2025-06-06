namespace AutoPatrol.Asserts
{
    public class PromptMessage
    {
        /// <summary>
        /// 设备IP地址无法PING通
        /// </summary>
        public static readonly string DEVICE_IP_ADDRESS_PING_FAILURE = "电脑IP地址无法PING通";

        /// <summary>
        /// 网关IP地址无法PING通
        /// </summary>
        public static readonly string GATEWAY_IP_ADDRESS_PING_FAILURE = "网关IP不通，机况异常";

        /// <summary>
        /// 设备关机
        /// </summary>
        public static readonly string DEVICE_SHUT_DOWN = "设备关机";

        /// <summary>
        /// 网线被拔
        /// </summary>
        public static readonly string ETHERNET_CABLE_PULLED_OUT = "网线被拔";

        /// <summary>
        /// IP地址变更
        /// </summary>
        public static readonly string IP_CHANGE = "IP地址变更";

        /// <summary>
        /// 访问共享路径失败
        /// </summary>
        public static readonly string ACCESS_PATH_FAILURE = "无法访问共享路径";

        /// <summary>
        /// 账号或密码错误
        /// </summary>
        public static readonly string ACCOUNT_OR_PASSWORD_ERROR = "账号或密码错误";

        /// <summary>
        /// 密码过期
        /// </summary>
        public static readonly string PASSWORD_OVERDUE = "密码过期";

        /// <summary>
        /// 当前账号无访问权限
        /// </summary>
        public static readonly string ACCOUNT_NOT_ACCESS_PERMISSIONS = "当前账号无访问权限";

        /// <summary>
        /// 使用不同凭据同时访问同一服务器
        /// </summary>
        public static readonly string MULTIPLE_LOGONS = "使用不同凭据同时访问同一服务器";

        /// <summary>
        /// 当天日志不存在
        /// </summary>
        public static readonly string DAILY_LOG_NOT_EXIST = "当天日志不存在";

        /// <summary>
        /// 日志路径变更
        /// </summary>
        public static readonly string LOG_PATH_CHANGE = "日志路径变更";

        /// <summary>
        /// 设备未生产
        /// </summary>
        public static readonly string DEVICE_NOT_PRODUCED = "设备未生产";

        /// <summary>
        /// 设备被拆除
        /// </summary>
        public static readonly string DEVICE_REMOVAL = "设备被拆除";

        /// <summary>
        /// 连接超时
        /// </summary>
        public static readonly string CONNECTION_TIMED_OUT = "连接超时";

        /// <summary>
        /// 网络繁忙
        /// </summary>
        public static readonly string NETWORK_BUSY = "网络繁忙";

        /// <summary>
        /// 找不到网络名
        /// </summary>
        public static readonly string NETWORK_NOT_FOUND = "找不到网络名";

        /// <summary>
        /// 目标服务器不可到达
        /// </summary>
        public static readonly string TARGET_SERVER_NOT_REACHABLE = "目标服务器不可到达";

        /// <summary>
        /// 共享名称错误
        /// </summary>
        public static readonly string SHARE_NAME_ERROR = "共享名称错误";
    }
}
