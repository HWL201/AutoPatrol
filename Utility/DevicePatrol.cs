using AutoPatrol.Asserts;
using AutoPatrol.Models;
using Microsoft.AspNetCore.Hosting;
using OfficeOpenXml;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using LicenseContext = OfficeOpenXml.LicenseContext;

namespace AutoPatrol.Utility
{
    public static class DevicePatrol
    {
        /// <summary>
        /// 对特定项目进行巡检
        /// </summary>
        /// <param name="deviceList"></param>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public static async Task Patrol(List<DeviceViewModel> allDeviceList, string filePath) {
            // 设置EPPlus许可证（非商业用途）
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            var stream = new MemoryStream();
            using (var package = new ExcelPackage(stream)) {
                var worksheet = package.Workbook.Worksheets.Add("巡检结果");
                worksheet.Cells[1, 1].Value = "产线";
                worksheet.Cells[1, 2].Value = "线体顺序";
                worksheet.Cells[1, 3].Value = "设备类型";
                worksheet.Cells[1, 4].Value = "设备编码";
                worksheet.Cells[1, 5].Value = "设备IP";
                worksheet.Cells[1, 6].Value = "巡检项目";
                worksheet.Cells[1, 7].Value = "巡检结论";
                worksheet.Cells[1, 8].Value = "异常描述";
                worksheet.Cells[1, 9].Value = "提示信息";
                int rowIndex = 2;

                // 过滤IP为空的数据
                var deviceList = allDeviceList.Where(a => a.Ip != "/").ToList();

                #region IP巡检

                var pingResult = await PingIPList(deviceList.Select(a => a.Ip).ToList());

                foreach (var device in deviceList) {

                    bool isPingSuccess;

                    string? result;
                    string? describe;
                    List<string> message = new List<string>();

                    if (DriverClassify.TypeJudge(device.DriverName) == "机况") {
                        if (pingResult.TryGetValue(device.Ip, out isPingSuccess) && isPingSuccess) {
                            result = "成功";
                            describe = "无异常";
                            message.Add("");
                        }
                        else {
                            result = "失败";
                            describe = PromptMessage.IP_ADDRESS_PING_FAILURE;
                            message.Add(PromptMessage.DEVICE_REMOVAL);
                            message.Add(PromptMessage.DEVICE_SHUT_DOWN);
                            message.Add(PromptMessage.ETHERNET_CABLE_PULLED_OUT);
                            message.Add(PromptMessage.IP_CHANGE);
                        }

                        worksheet.Cells[rowIndex, 1].Value = device.Line;
                        worksheet.Cells[rowIndex, 2].Value = device.Num;
                        worksheet.Cells[rowIndex, 3].Value = device.DeviceType;
                        worksheet.Cells[rowIndex, 4].Value = device.Code;
                        worksheet.Cells[rowIndex, 5].Value = device.Ip;
                        worksheet.Cells[rowIndex, 6].Value = "机况";
                        worksheet.Cells[rowIndex, 7].Value = result;
                        worksheet.Cells[rowIndex, 8].Value = describe;
                        worksheet.Cells[rowIndex, 9].Value = string.Join('，', message);
                        rowIndex++;
                    }
                    else {
                        if (pingResult.TryGetValue(device.Ip, out isPingSuccess) && isPingSuccess) {
                            result = "成功";
                            describe = "无异常";
                            message.Add("");
                        }
                        else {
                            result = "失败";
                            describe = PromptMessage.IP_ADDRESS_PING_FAILURE;
                            message.Add(PromptMessage.DEVICE_SHUT_DOWN);
                            message.Add(PromptMessage.ETHERNET_CABLE_PULLED_OUT);
                            message.Add(PromptMessage.IP_CHANGE);
                        }

                        worksheet.Cells[rowIndex, 1].Value = device.Line;
                        worksheet.Cells[rowIndex, 2].Value = device.Num;
                        worksheet.Cells[rowIndex, 3].Value = device.DeviceType;
                        worksheet.Cells[rowIndex, 4].Value = device.Code;
                        worksheet.Cells[rowIndex, 5].Value = device.Ip;
                        worksheet.Cells[rowIndex, 6].Value = "IP";
                        worksheet.Cells[rowIndex, 7].Value = result;
                        worksheet.Cells[rowIndex, 8].Value = describe;
                        worksheet.Cells[rowIndex, 9].Value = string.Join('，', message);
                        rowIndex++;
                    }
                }

                #endregion

                #region 共享路径巡检

                // 过滤机况类型，剩下数据类型
                var dataList = deviceList.Where(a => a.DriverType == "数据").ToList();

                var accessSharePath = await ConnectToSharesAsync(pingResult, dataList);  // 默认10并发
                //var accessSharePath = await ConnectToSharesAsync(pingResult, dataList, 8);  // 8并发
                //var accessSharePath = await ConnectToSharesAsync(pingResult, dataList, 6);  // 8并发
                //var accessSharePath = await ConnectToSharesAsync(pingResult, dataList, 4);  // 8并发

                foreach (var device in dataList) {
                    ConnectionResult connResult;

                    string? result;

                    if (accessSharePath.TryGetValue(device.Path, out connResult) && connResult.Status == ConnectionStatus.Success) {
                        result = "成功";
                    }
                    else if (connResult.Status == ConnectionStatus.Failure && connResult.Profile == "无异常") {
                        result = "其他";
                    }
                    else {
                        result = "失败";
                    }

                    worksheet.Cells[rowIndex, 1].Value = device.Line;
                    worksheet.Cells[rowIndex, 2].Value = device.Num;
                    worksheet.Cells[rowIndex, 3].Value = device.DeviceType;
                    worksheet.Cells[rowIndex, 4].Value = device.Code;
                    worksheet.Cells[rowIndex, 5].Value = device.Ip;
                    worksheet.Cells[rowIndex, 6].Value = "共享路径";
                    worksheet.Cells[rowIndex, 7].Value = result;
                    worksheet.Cells[rowIndex, 8].Value = connResult.Profile;
                    worksheet.Cells[rowIndex, 9].Value = connResult.Message;
                    rowIndex++;
                }

                #endregion

                package.Save();
            }

            FileOperation.WriteExcel(stream, filePath);
        }


        /// <summary>
        /// 异步Ping IP 地址集合并分别返回结果
        /// </summary>
        /// <param name="ipList">IP地址集合</param>
        /// <returns></returns>
        public static async Task<Dictionary<string, bool>> PingIPList(List<string> ipList) {
            Dictionary<string, bool> pingResults = new Dictionary<string, bool>();

            // 创建所有Ping任务
            var tasks = ipList.Select(async ip => {
                try {
                    using var ping = new Ping();
                    var reply = await ping.SendPingAsync(ip, 1000);
                    return new { IP = ip, Success = reply.Status == IPStatus.Success };
                }
                catch {
                    return new { IP = ip, Success = false };
                }
            }).ToList();

            // 并行等待所有任务完成
            var results = await Task.WhenAll(tasks);

            // 将结果存入字典
            foreach (var result in results) {
                pingResults[result.IP] = result.Success;
            }

            return pingResults;
        }


        #region 提供网络共享路径连接和管理功能的工具，使用 Windows API (WNetAddConnection2) 实现高效连接

        /// <summary>
        /// 并行连接多个共享路径，支持并发控制
        /// </summary>
        /// <param name="pingResult">设备IP地址PING结果</param>
        /// <param name="devices">设备信息</param>
        /// <param name="maxConcurrency">最大并发连接数，默认10</param>
        /// <returns>包含每个路径连接结果的字典</returns>
        public static async Task<Dictionary<string, ConnectionResult>> ConnectToSharesAsync(Dictionary<string, bool> pingResult, IEnumerable<DeviceViewModel> devices, int maxConcurrency = 10) {
            var results = new Dictionary<string, ConnectionResult>();
            DateTime today = DateTime.Today;

            // 使用信号量控制最大并发数，防止资源耗尽
            var semaphore = new SemaphoreSlim(maxConcurrency); 

            // 为每个共享路径创建异步任务
            var tasks = devices.Select(async device => {
                // 等待信号量，确保并发数不超过限制
                await semaphore.WaitAsync();

                string ip = device.Ip;
                string path = device.Path;
                string account = device.Account == "/" ? "" : device.Account;
                string password = device.Password == "/" ? "" : device.Password;
                string postfix = device.Postfix == "/" ? "" : device.Postfix;

                try {
                    if (path == "/") {
                        results[path] = new ConnectionResult {
                            Status = ConnectionStatus.Failure,
                            Profile = "无异常",
                            Message = "数据采集不依赖共享文件"
                        };
                    }
                    else if (path == "待排查") {
                        results[path] = new ConnectionResult {
                            Status = ConnectionStatus.Failure,
                            Profile = "无异常",
                            Message = "共享文件路径未填入"
                        };
                    }
                    // 如果设备IP是PING通的
                    else if (pingResult[ip]) {

                        //string path = device.Path;
                        //string account = device.Account == "/" ? "" : device.Account;
                        //string password = device.Password == "/" ? "" : device.Password;
                        //string postfix = device.Postfix == "/" ? "" : device.Account;
                        int floor = int.Parse(device.Floor);
                        // 如果是印刷机，需要额外的处理逻辑
                        if (device.DriverName == "CQ.IOT.HT.PanasonicPrintingDriver.dll") {
                            path = path.Replace("*", today.ToString("yyyy-MM-dd"));
                        }
                        // 执行连接操作
                        bool success = await ConnectToShareAsync(path, account, password);

                        if (success) {
                            try {
                                // 获取目录中的所有文件
                                bool exist = FileOperation.IsFileExists(path, today, postfix, floor);

                                if (exist) {
                                    results[device.Path] = new ConnectionResult {
                                        Status = ConnectionStatus.Success,
                                        Profile = "无异常",
                                        Message = ""
                                    };
                                }
                                else {
                                    List<string> message = new List<string>() {
                                        PromptMessage.DEVICE_NOT_PRODUCED,
                                        PromptMessage.LOG_PATH_CHANGE,
                                    };
                                    results[device.Path] = new ConnectionResult {
                                        Status = ConnectionStatus.Success,
                                        Profile = PromptMessage.DAILY_LOG_NOT_EXIST,
                                        Message = string.Join("，", message),
                                    };
                                }
                            }
                            finally {
                                DisconnectShare(path, true);
                            }
                        }
                        else {
                            results[device.Path] = new ConnectionResult {
                                Status = ConnectionStatus.UnknownError,
                                Profile = PromptMessage.DAILY_LOG_NOT_EXIST,
                                Message = "未知原因",
                            };
                        }
                    }
                    else {
                        List<string> message = new List<string>() {
                            PromptMessage.DEVICE_SHUT_DOWN,
                            PromptMessage.ETHERNET_CABLE_PULLED_OUT,
                            PromptMessage.IP_CHANGE,
                        };
                        // 使用device.Path而不是局部变量path，是因为要基于源路径对字典进行搜索，印刷机的path是动态拼接的
                        results[device.Path] = new ConnectionResult { Status = ConnectionStatus.Failure, Profile = PromptMessage.ACCESS_PATH_FAILURE, Message = string.Join('，', message) };
                    }
                }
                catch (Win32Exception ex) {
                    results[device.Path] = MapWin32ErrorCodeToStatus(ex.NativeErrorCode);
                }
                catch (Exception ex) {
                    results[device.Path] = new ConnectionResult {
                        Status = ConnectionStatus.UnknownError,
                        Profile = PromptMessage.ACCESS_PATH_FAILURE,
                        Message = ex.Message,
                    };
                }
                finally {
                    // 释放信号量，允许下一个任务执行
                    semaphore.Release();
                }
            });

            // 等待所有任务完成
            await Task.WhenAll(tasks);
            return results;
        }

        /// <summary>
        /// 异步连接到指定的网络共享路径
        /// </summary>
        /// <param name="sharePath">共享路径 (格式: \\server\share)</param>
        /// <param name="username">用于身份验证的用户名</param>
        /// <param name="password">用于身份验证的密码</param>
        /// <returns>连接成功返回true，失败返回false</returns>
        public static async Task<bool> ConnectToShareAsync(string sharePath, string username, string password) {
            // 使用 Task.Run 将同步操作转换为异步执行
            return await Task.Run(() => ConnectToShare(sharePath, username, password));
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

            return result == 0;
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
        private static ConnectionResult MapWin32ErrorCodeToStatus(int errorCode) {
            switch (errorCode) {
                case 5:     // ERROR_ACCESS_DENIED
                    return new ConnectionResult() {
                        Status = ConnectionStatus.PermissionDenied,
                        Profile = PromptMessage.ACCESS_PATH_FAILURE,
                        Message = PromptMessage.ACCOUNT_NOT_ACCESS_PERMISSIONS,
                    };
                case 1219:  // ERROR_MULTIPLE_LOGONS
                    return new ConnectionResult() {
                        Status = ConnectionStatus.NetworkError,
                        Profile = PromptMessage.ACCESS_PATH_FAILURE,
                        Message = PromptMessage.MULTIPLE_LOGONS,
                    };
                case 1326:  // ERROR_LOGON_FAILURE
                    return new ConnectionResult() {
                        Status = ConnectionStatus.InvalidCredentials,
                        Profile = PromptMessage.ACCESS_PATH_FAILURE,
                        Message = PromptMessage.ACCOUNT_OR_PASSWORD_ERROR,
                    };
                case 53:    // ERROR_BAD_NETPATH
                    return new ConnectionResult() {
                        Status = ConnectionStatus.PathNotFound,
                        Profile = PromptMessage.ACCESS_PATH_FAILURE,
                        Message = PromptMessage.LOG_PATH_CHANGE,
                    };
                case 67:    // ERROR_NETNAME_DELETED
                    return new ConnectionResult() {
                        Status = ConnectionStatus.PathNotFound,
                        Profile = PromptMessage.ACCESS_PATH_FAILURE,
                        Message = "共享名称被删除或服务器暂时无法访问",
                    };
                default:
                    return new ConnectionResult() {
                        Status = ConnectionStatus.UnknownError,
                        Profile = PromptMessage.ACCESS_PATH_FAILURE,
                        Message = $"未知错误（代码：{errorCode}）：请上网排查。",
                    };
            }
        }

        #endregion
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
        UnknownError            // 未知错误
    }
}