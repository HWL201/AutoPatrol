using AutoPatrol.Asserts;
using AutoPatrol.Models;
using Microsoft.AspNetCore.Hosting;
using OfficeOpenXml;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
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
            // 获取最近一份记录
            List<ResultViewModel> lastRecords = new List<ResultViewModel>();

            string rootPath = Path.GetDirectoryName(filePath);
            var lastFile = new DirectoryInfo(rootPath)
                .GetFiles("*.xlsx", SearchOption.TopDirectoryOnly)
                .Concat(new DirectoryInfo(rootPath).GetFiles("*.xls", SearchOption.TopDirectoryOnly))
                .Where(file => file.Name.Contains("自动巡检"))
                .OrderByDescending(file => file.LastWriteTime)
                .FirstOrDefault();

            if(lastFile != null) {
                lastRecords = FileOperation.ReadExcel(lastFile.FullName);
            }
            
            string? lastDateStr = lastFile?.Name.Substring(0, 8); // 从文件名提取出日期字符串

            // 以多级字典存储
            foreach (var record in lastRecords) {
                DataManager.AddRecord(record);
            }

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
                worksheet.Cells[1, 10].Value = "持续天数";
                int rowIndex = 2;

                // 过滤IP为空的数据
                var deviceList = allDeviceList.Where(a => a.Ip != "/").ToList();

                #region IP巡检（机况IP和设备IP）

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

                        // 当前检查项的上一条记录
                        var lastRecord = DataManager.GetRecord(device.Line, device.Code, "机况");
                        int duration = 1;   // 默认持续天数为1

                        // 如果上一份记录存在
                        if(lastRecord != null) {
                            duration = CompareRecord(result, lastRecord.Result, DateTime.Now.ToString("yyyyMMdd"), lastDateStr, lastRecord.Duration);
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
                        worksheet.Cells[rowIndex, 10].Value = duration;
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

                        // 当前检查项的上一条记录
                        var lastRecord = DataManager.GetRecord(device.Line, device.Code, "IP");
                        int duration = 1;   // 默认持续天数为1

                        // 如果上一份记录存在
                        if (lastRecord != null) {
                            duration = CompareRecord(result, lastRecord.Result, DateTime.Now.ToString("yyyyMMdd"), lastDateStr, lastRecord.Duration);
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

                var accessSharePath = await ConnectToSharesAsync(pingResult, dataList, 5);  // 默认10并发
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

                    // 当前检查项的上一条记录
                    var lastRecord = DataManager.GetRecord(device.Line, device.Code, "共享路径");
                    int duration = 1;   // 默认持续天数为1

                    // 如果上一份记录存在
                    if (lastRecord != null) {
                        duration = CompareRecord(result, lastRecord.Result, DateTime.Now.ToString("yyyyMMdd"), lastDateStr, lastRecord.Duration);
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
                    worksheet.Cells[rowIndex, 10].Value = duration;
                    rowIndex++;
                }

                #endregion

                package.Save();
            }

            FileOperation.WriteExcel(stream, filePath);
        }


        /// <summary>
        /// 根据两份记录判断持续天数是否需要加1
        /// </summary>
        /// <param name="result">当前结论</param>
        /// <param name="lastResult">上一份结论</param>
        /// <param name="nowDate">当前日期</param>
        /// <param name="lastDate">上一个日期</param>
        /// <param name="lastDuration">上一个持续时间</param>
        /// <returns></returns>
        private static int CompareRecord(string result, string lastResult, string nowDate, string lastDate, int lastDuration) {
            if (result == lastResult) {
                return nowDate == lastDate ? lastDuration : ++lastDuration;
            }
            else {
                return 1;
            }
        }


        /// <summary>
        /// 异步Ping IP 地址集合并分别返回结果
        /// </summary>
        /// <param name="ipList">IP地址集合</param>
        /// <returns></returns>
        private static async Task<Dictionary<string, bool>> PingIPList(List<string> ipList) {
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


        /// <summary>
        /// 并行连接多个共享路径，支持并发控制
        /// </summary>
        /// <param name="pingResult">设备IP地址PING结果</param>
        /// <param name="devices">设备信息</param>
        /// <param name="maxConcurrency">最大并发连接数，默认10</param>
        /// <returns>包含每个路径连接结果的字典</returns>
        private static async Task<Dictionary<string, ConnectionResult>> ConnectToSharesAsync(Dictionary<string, bool> pingResult, IEnumerable<DeviceViewModel> devices, int maxConcurrency = 10) {
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
                        int floor = int.Parse(device.Floor);
                        // 如果是印刷机，需要额外的处理逻辑
                        if (device.DriverName == "CQ.IOT.HT.PanasonicPrintingDriver.dll") {
                            path = path.Replace("*", today.ToString("yyyy-MM-dd"));
                        }
                        // 执行连接操作

                        Console.WriteLine($"{device.Line} {device.Code} {path} 执行共享连接");
                        Stopwatch stopwatch = Stopwatch.StartNew();
                        bool success = await NetOperation.ConnectToShareAsync(path, account, password);
                        stopwatch.Stop();
                        Console.WriteLine($"连接耗时: {stopwatch.ElapsedMilliseconds} 毫秒");

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
                                // 断开共享连接
                                Console.WriteLine($"{device.Line} {device.Code} {path} 断开共享连接");
                                NetOperation.DisconnectShare(path, true);
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
                    results[device.Path] = NetOperation.MapWin32ErrorCodeToStatus(ex.NativeErrorCode);
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
    }
}