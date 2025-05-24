using AutoPatrol.Asserts;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace AutoPatrol.Utility
{
    /// <summary>
    /// 提供网络共享路径连接和管理功能的工具，使用 Windows API (WNetAddConnection2) 实现高效连接
    /// </summary>
    public class NetOperation
    {
        /// <summary>
        /// 异步连接到指定的网络共享路径
        /// </summary>
        /// <param name="sharePath">共享路径 (格式: \\server\share)</param>
        /// <param name="username">用于身份验证的用户名</param>
        /// <param name="password">用于身份验证的密码</param>
        /// <returns>连接成功返回true，失败返回false</returns>
        public static async Task<bool> ConnectToShareAsync(string sharePath, string username, string password) {
            // 使用 Task.Run 将同步操作转换为异步执行
            // return await Task.Run(() => ConnectToShare(sharePath, username, password));

            /*  同步函数异步调用，可能无法取消
            var cts = new CancellationTokenSource(3000);
            try {
                return await Task.Run(() => ConnectToShare(sharePath, username, password), cts.Token);
            }
            catch (OperationCanceledException) {
                WNetCancelConnection2(sharePath, 0, true);
                return false;
            }
            */

            /*var connectTask = Task.Run(() => ConnectToShare(sharePath, username, password));

            try {
                // 等待连接任务完成或超时
                if (await Task.WhenAny(connectTask, Task.Delay(5000)) != connectTask) {
                    // 如果不是连接任务先完成，说明超时了
                    DisconnectShare(sharePath, true);
                    return false;
                }

                // 连接任务已完成，获取结果
                return await connectTask;
            }
            catch {
                throw;
            }*/

            try {
                var connectTask = Task.Run(() => ConnectToShare(sharePath, username, password));
                var timeoutTask = Task.Delay(10000);
                var completedTask = await Task.WhenAny(connectTask, timeoutTask);   // completedTask 指向最先完成的任务

                if (completedTask == timeoutTask) { // 如果超时
                    DisconnectShare(sharePath, true);
                    return false;
                }

                return await connectTask;
            }
            catch {
                throw;
            }
        }


        /// <summary>
        /// 同步连接到指定的网络共享路径
        /// </summary>
        /// <param name="sharePath">共享路径 (格式: \\server\share)</param>
        /// <param name="username">用于身份验证的用户名</param>
        /// <param name="password">用于身份验证的密码</param>
        /// <returns>连接成功返回true，失败返回false</returns>
        public static bool ConnectToShare(string sharePath, string username, string password) {
            // 参数验证
            if (string.IsNullOrEmpty(sharePath))
                throw new ArgumentNullException(nameof(sharePath));

            // 初始化网络资源结构
            var netResource = new NetResource {
                Scope = ResourceScope.GlobalNetwork,        // 连接到全局网络资源
                ResourceType = ResourceType.Disk,           // 资源类型为磁盘共享
                DisplayType = ResourceDisplaytype.Share,    // 显示为共享文件夹
                RemoteName = sharePath                      // 设置远程路径
            };

            // 调用 Windows API 建立连接
            // 返回值为0表示成功，非零值为错误代码
            int result = WNetAddConnection2(
                netResource,
                password,
                username,
                0); // 0表示默认标志，不保存连接

            // return result == 0;
            // 如果连接失败，抛出 Win32Exception
            if (result != 0)
                throw new Win32Exception(result);

            return true;
        }


        /// <summary>
        /// 断开与指定共享路径的连接
        /// </summary>
        /// <param name="sharePath">要断开的共享路径</param>
        /// <param name="force">是否强制断开，即使有打开的文件</param>
        /// <returns>断开成功返回true，失败返回false</returns>
        public static bool DisconnectShare(string sharePath, bool force = false) {
            // 调用 Windows API 断开连接
            int result = WNetCancelConnection2(sharePath, 0, force);
            return result == 0;
        }


        /// <summary>
        /// 导入 Windows API 方法：建立网络连接
        /// </summary>
        /// <param name="lpNetResource">网络资源信息</param>
        /// <param name="lpPassword">密码</param>
        /// <param name="lpUserName">用户名</param>
        /// <param name="dwFlags">连接标志</param>
        /// <returns></returns>
        [DllImport("mpr.dll")]
        private static extern int WNetAddConnection2(NetResource lpNetResource, string lpPassword, string lpUserName, int dwFlags);


        /// <summary>
        /// 导入 Windows API 方法：取消网络连接
        /// </summary>
        /// <param name="lpName">共享路径</param>
        /// <param name="dwFlags">操作标志</param>
        /// <param name="fForce">是否强制断开</param>
        [DllImport("mpr.dll")]
        private static extern int WNetCancelConnection2(string lpName, int dwFlags, bool fForce);


        /// <summary>
        /// 将 Win32 错误代码映射为自定义连接状态枚举
        /// </summary>
        /// <param name="errorCode">Win32错误代码</param>
        /// <returns>对应的连接状态</returns>
        public static ConnectionResult MapWin32ErrorCodeToStatus(int errorCode) {
            switch (errorCode) {
                //case 5:     // 无访问权限
                //    return new ConnectionResult() {
                //        Status = ConnectionStatus.PermissionDenied,
                //        Profile = PromptMessage.ACCESS_PATH_FAILURE,
                //        Message = PromptMessage.ACCOUNT_NOT_ACCESS_PERMISSIONS,
                //    };

                case 55:    //  网络资源繁忙
                    return new ConnectionResult() {
                        Status = ConnectionStatus.NetworkBusy,
                        Profile = PromptMessage.ACCESS_PATH_FAILURE,
                        Message = PromptMessage.NETWORK_BUSY,
                    };
                case 67:    //  日志路径变更
                    return new ConnectionResult() {
                        Status = ConnectionStatus.PathNotFound,
                        Profile = PromptMessage.ACCESS_PATH_FAILURE,
                        Message = PromptMessage.LOG_PATH_CHANGE,
                    };
                case 86:    // 账号或密码错误
                    return new ConnectionResult() {
                        Status = ConnectionStatus.InvalidCredentials,
                        Profile = PromptMessage.ACCESS_PATH_FAILURE,
                        Message = PromptMessage.ACCOUNT_OR_PASSWORD_ERROR,
                    };
                case 1219:  // 多凭据重复登录
                    return new ConnectionResult() {
                        Status = ConnectionStatus.NetworkError,
                        Profile = PromptMessage.ACCESS_PATH_FAILURE,
                        Message = PromptMessage.MULTIPLE_LOGONS,
                    };
                case 1326:  // 密码过期
                    return new ConnectionResult() {
                        Status = ConnectionStatus.PasswordOverdue,
                        Profile = PromptMessage.ACCESS_PATH_FAILURE,
                        Message = PromptMessage.PASSWORD_OVERDUE,
                    };
                case 1327:  // 无访问权限
                    return new ConnectionResult() {
                        Status = ConnectionStatus.PermissionDenied,
                        Profile = PromptMessage.ACCESS_PATH_FAILURE,
                        Message = PromptMessage.ACCOUNT_NOT_ACCESS_PERMISSIONS,
                    };
                //case 53:
                //    return new ConnectionResult() {
                //        Status = ConnectionStatus.PathNotFound,
                //        Profile = PromptMessage.ACCESS_PATH_FAILURE,
                //        Message = PromptMessage.LOG_PATH_CHANGE,
                //    };
                //case 1330:
                //    return new ConnectionResult() {
                //        Status = ConnectionStatus.PasswordOverdue,
                //        Profile = PromptMessage.ACCESS_PATH_FAILURE,
                //        Message = PromptMessage.PASSWORD_OVERDUE,
                //    };
                default:
                    return new ConnectionResult() {
                        Status = ConnectionStatus.UnknownError,
                        Profile = PromptMessage.ACCESS_PATH_FAILURE,
                        Message = $"未知错误（代码：{errorCode}）：请上网排查。",
                    };
            }
        }
    }

    /// <summary>
    /// Windows API P/Invoke 所需的结构和方法定义
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public class NetResource
    {
        public ResourceScope Scope;         // 资源范围（全局网络、已连接等）
        public ResourceType ResourceType;   // 资源类型（磁盘、打印机等）
        public ResourceDisplaytype DisplayType; // 显示类型（共享、服务器等）
        public int Usage;                   // 资源使用选项
        public string LocalName;            // 本地设备名（如 Z:）
        public string RemoteName;           // 远程共享路径
        public string Comment;              // 资源注释
        public string Provider;             // 网络提供者名称
    }


    /// <summary>
    /// 资源范围枚举
    /// </summary>
    public enum ResourceScope
    {
        Connected = 1,      // 已连接的资源
        GlobalNetwork,      // 全局网络资源
        Remembered,         // 记住的连接
        Recent,             // 最近使用的连接
        Context             // 基于当前上下文的资源
    }


    /// <summary>
    /// 资源类型枚举
    /// </summary>
    public enum ResourceType
    {
        Any = 0,            // 任何类型
        Disk = 1,           // 磁盘共享
        Print = 2,          // 打印机共享
        Reserved = 8,       // 保留值
    }


    /// <summary>
    /// 资源显示类型枚举
    /// </summary>
    public enum ResourceDisplaytype
    {
        Generic = 0x0,      // 通用类型
        Domain = 0x01,      // 域
        Server = 0x02,      // 服务器
        Share = 0x03,       // 共享文件夹
        File = 0x04,        // 文件
        Group = 0x05,       // 工作组
        Network = 0x06,     // 网络
        Root = 0x07,        // 网络根目录
        Shareadmin = 0x08,  // 管理共享
        Directory = 0x09,   // 目录
        Tree = 0x0a,        // 树结构
        Ndscontainer = 0x0b // NDS容器
    }


    /// <summary>
    /// 表示网络共享连接结果的模型
    /// </summary>
    public class ConnectionResult
    {
        public ConnectionStatus Status { get; set; }       // 连接状态
        public string? Profile { get; set; }
        public string? Message { get; set; }
    }


    /// <summary>
    /// 网络共享连接状态枚举
    /// </summary>
    public enum ConnectionStatus
    {
        Success,                // 连接成功
        Failure,                // 连接失败
        InvalidCredentials,     // 凭据无效
        PathNotFound,           // 路径不存在
        PermissionDenied,       // 权限被拒绝
        NetworkError,           // 网络错误
        PasswordOverdue,        // 密码过期
        NetworkBusy,            // 网络繁忙
        UnknownError,            // 未知错误
    }
}
