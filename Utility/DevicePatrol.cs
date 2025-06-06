using AutoPatrol.Asserts;
using AutoPatrol.Models;
using Microsoft.AspNetCore.Hosting;
using OfficeOpenXml;
using Serilog;
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
        public static async Task<List<ResultViewModel>> Patrol(List<DeviceViewModel> allDeviceList, string filePath) {
            // 获取最近一份记录
            List<ResultViewModel> lastRecords = new List<ResultViewModel>();
            List<ResultViewModel> nextRecords = new List<ResultViewModel>();

            string rootPath = Path.GetDirectoryName(filePath);
            Directory.CreateDirectory(rootPath);
            var lastFile = new DirectoryInfo(rootPath)
                .GetFiles("*.xlsx", SearchOption.TopDirectoryOnly)
                .Concat(new DirectoryInfo(rootPath).GetFiles("*.xls", SearchOption.TopDirectoryOnly))
                .Where(file => file.Name.Contains("自动巡检"))
                .OrderByDescending(file => file.LastWriteTime)
                .FirstOrDefault();

            if (lastFile != null) {
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
                // int rowIndex = 2;

                // 过滤IP为空的数据
                var deviceList = allDeviceList.Where(a => a.Ip != "/").ToList();

                #region 网关IP和设备IP巡检

                var pingResult = await PingIPList(deviceList.Select(a => a.Ip).ToList());

                foreach (var device in deviceList) {

                    bool isPingSuccess;

                    string? result;
                    string? describe;
                    List<string> message = new List<string>();

                    // 网关IP
                    if (DriverClassify.TypeJudge(device.DriverName) == "机况") {
                        if (pingResult.TryGetValue(device.Ip, out isPingSuccess) && isPingSuccess) {
                            result = "成功";
                            describe = "网关IP正常";
                            message.Add("正常");
                        }
                        else {
                            result = "失败";
                            describe = PromptMessage.GATEWAY_IP_ADDRESS_PING_FAILURE;
                            message.Add(PromptMessage.DEVICE_REMOVAL);
                            message.Add(PromptMessage.DEVICE_SHUT_DOWN);
                            message.Add(PromptMessage.ETHERNET_CABLE_PULLED_OUT);
                            message.Add(PromptMessage.IP_CHANGE);
                        }

                        // 当前检查项的上一条记录
                        var lastRecord = DataManager.GetRecord(device.Line, device.Code, "机况");
                        int duration = 1;   // 默认持续天数为1

                        // 如果上一份记录存在
                        if (lastRecord != null) {
                            duration = CompareRecord(result, lastRecord.Result, DateTime.Now.ToString("yyyyMMdd"), lastDateStr, lastRecord.Duration);
                        }

                        nextRecords.Add(new ResultViewModel() {
                            Line = device.Line,
                            Num = device.Num,
                            DeviceType = device.DeviceType,
                            Code = device.Code,
                            Ip = device.Ip,
                            Item = "机况",
                            Result = result,
                            Describe = describe,
                            Message = string.Join('，', message),
                            Duration = duration,
                        });
                    }
                    // 设备IP
                    else {
                        if (pingResult.TryGetValue(device.Ip, out isPingSuccess) && isPingSuccess) {
                            result = "成功";
                            describe = "电脑IP正常";
                            message.Add("IP正常");
                        }
                        else {
                            result = "失败";
                            describe = PromptMessage.DEVICE_IP_ADDRESS_PING_FAILURE;
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

                        nextRecords.Add(new ResultViewModel() {
                            Line = device.Line,
                            Num = device.Num,
                            DeviceType = device.DeviceType,
                            Code = device.Code,
                            Ip = device.Ip,
                            Item = "IP",
                            Result = result,
                            Describe = describe,
                            Message = string.Join('，', message),
                            Duration = duration,
                        });
                    }
                }

                #endregion

                #region 账号密码和共享路径巡检

                // 过滤机况类型行，剩下数据类型行
                var dataList = deviceList.Where(a => a.DriverType == "数据").ToList();
                // 根据PING结果访问共享路径
                var accessSharePath = await ConnectToSharesAsync(pingResult, dataList, 5);  // 默认10并发

                foreach (var device in dataList) {
                    ConnectionResult connResult;

                    // 如果有检测到共享文件，说明账号密码和共享路径都正常
                    if (accessSharePath.TryGetValue(device.Path, out connResult) && connResult.Status == ConnectionStatus.Success) {
                        nextRecords.Add(GenerateRecord(device, lastDateStr, "账号密码", "成功", "账号密码正常", "账号密码正常"));
                        nextRecords.Add(GenerateRecord(device, lastDateStr, "共享路径", "成功", "共享路径访问正常", "共享路径访问正常"));
                        /*var lastRecord = DataManager.GetRecord(device.Line, device.Code, "账号密码");
                        int duration = 1;   // 默认持续天数为1
                        // 如果上一份记录存在
                        if (lastRecord != null) {
                            duration = CompareRecord("成功", lastRecord.Result, DateTime.Now.ToString("yyyyMMdd"), lastDateStr, lastRecord.Duration);
                        }

                        nextRecords.Add(new ResultViewModel() {
                            Line = device.Line,
                            Num = device.Num,
                            DeviceType = device.DeviceType,
                            Code = device.Code,
                            Ip = device.Ip,
                            Item = "账号密码",
                            Result = "成功",
                            Describe = "账号密码正常",
                            Message = "账号密码正常",
                            Duration = duration,
                        });

                        lastRecord = DataManager.GetRecord(device.Line, device.Code, "账号密码");
                        duration = 1;
                        if (lastRecord != null) {
                            duration = CompareRecord("成功", lastRecord.Result, DateTime.Now.ToString("yyyyMMdd"), lastDateStr, lastRecord.Duration);
                        }

                        nextRecords.Add(new ResultViewModel() {
                            Line = device.Line,
                            Num = device.Num,
                            DeviceType = device.DeviceType,
                            Code = device.Code,
                            Ip = device.Ip,
                            Item = "共享路径",
                            Result = "成功",
                            Describe = "共享路径访问正常",
                            Message = "共享路径访问正常",
                            Duration = duration,
                        });*/
                    }
                    else if (connResult != null) {  // IP地址PING不通不会去执行共享连接，也就不会存进共享连接字典集合，所以connResult为null
                        // 如果巡检项目是共享路径，需要额外添加账号密码的正常记录
                        if (connResult.Profile == PromptMessage.DAILY_LOG_NOT_EXIST || connResult.Profile == PromptMessage.ACCESS_PATH_FAILURE) {
                            nextRecords.Add(GenerateRecord(device, lastDateStr, "账号密码", "成功", "账号密码正常", "账号密码正常"));
                            nextRecords.Add(GenerateRecord(device, lastDateStr, "共享路径", "失败", connResult.Profile, connResult.Message));
                        }
                        else {
                            nextRecords.Add(GenerateRecord(device, lastDateStr, "账号密码", "失败", connResult.Profile, connResult.Message));
                        }
                    }

                    /*// 当前检查项的上一条记录
                    var lastRecord = DataManager.GetRecord(device.Line, device.Code, "账号密码");
                    int duration = 1;   // 默认持续天数为1

                    // 如果上一份记录存在
                    if (lastRecord != null) {
                        duration = CompareRecord(result, lastRecord.Result, DateTime.Now.ToString("yyyyMMdd"), lastDateStr, lastRecord.Duration);
                    }

                    nextRecords.Add(new ResultViewModel() {
                        Line = device.Line,
                        Num = device.Num,
                        DeviceType = device.DeviceType,
                        Code = device.Code,
                        Ip = device.Ip,
                        Item = "账号密码",
                        Result = result,
                        Describe = connResult.Profile,
                        Message = connResult.Message,
                        Duration = duration,
                    });*/
                }

                #endregion

                // 执行最终写入
                var sortedRecords = nextRecords
                    .OrderBy(item => ExtractPrefix(item.Line))
                    .ThenBy(item => ExtractNumber(item.Line))
                    .ThenBy(item => item.Item)
                    .ToList();

                int rowIndex = 2;
                foreach (var record in sortedRecords) {
                    worksheet.Cells[rowIndex, 1].Value = record.Line;
                    worksheet.Cells[rowIndex, 2].Value = record.Num;
                    worksheet.Cells[rowIndex, 3].Value = record.DeviceType;
                    worksheet.Cells[rowIndex, 4].Value = record.Code;
                    worksheet.Cells[rowIndex, 5].Value = record.Ip;
                    worksheet.Cells[rowIndex, 6].Value = record.Item;
                    worksheet.Cells[rowIndex, 7].Value = record.Result;
                    worksheet.Cells[rowIndex, 8].Value = record.Describe;
                    worksheet.Cells[rowIndex, 9].Value = record.Message;
                    worksheet.Cells[rowIndex, 10].Value = record.Duration;
                    rowIndex++;
                }

                package.Save();
            }

            FileOperation.WriteExcel(stream, filePath);

            return nextRecords;
        }


        /// <summary>
        /// 提取产线字母前缀
        /// </summary>
        /// <param name="line"></param>
        /// <returns></returns>
        private static string ExtractPrefix(string line) {
            var match = Regex.Match(line, @"^([A-Za-z]+)");
            return match.Success ? match.Groups[1].Value.ToUpper() : "~";
        }


        /// <summary>
        /// 提取产线数字部分
        /// </summary>
        /// <param name="line"></param>
        /// <returns></returns>
        private static int ExtractNumber(string line) {
            var match = Regex.Match(line, @"^[A-Za-z]+(\d+)");
            return match.Success ? int.Parse(match.Groups[1].Value) : int.MaxValue;
        }


        /// <summary>
        /// 生成一条巡检记录
        /// </summary>
        /// <param name="device">设备模型</param>
        /// <param name="lastDateStr">上一次对应巡检记录的日期</param>
        /// <param name="item">巡检项目</param>
        /// <param name="result">巡检结论</param>
        /// <param name="describe">巡检描述</param>
        /// <param name="message">巡检信息</param>
        /// <returns></returns>
        private static ResultViewModel GenerateRecord(DeviceViewModel device, string lastDateStr, string item, string result, string describe, string message) {
            var lastRecord = DataManager.GetRecord(device.Line, device.Code, item);
            int duration = 1;   // 默认持续天数为1
                                // 如果上一份记录存在
            if (lastRecord != null) {
                duration = CompareRecord(result, lastRecord.Result, DateTime.Now.ToString("yyyyMMdd"), lastDateStr, lastRecord.Duration);
            }

            return new ResultViewModel() {
                Line = device.Line,
                Num = device.Num,
                DeviceType = device.DeviceType,
                Code = device.Code,
                Ip = device.Ip,
                Item = item,
                Result = result,
                Describe = describe,
                Message = message,
                Duration = duration,
            };
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
                            Profile = "正常",
                            Message = "数据采集不依赖共享文件"
                        };
                    }
                    else if (path == "待排查") {
                        results[path] = new ConnectionResult {
                            Status = ConnectionStatus.Failure,
                            Profile = "正常",
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
                        Log.Information($"{device.Line} {device.Code} {path} 执行共享连接");
                        Stopwatch stopwatch = Stopwatch.StartNew();
                        bool success = await NetOperation.ConnectToShareAsync(path, account, password);
                        stopwatch.Stop();
                        Log.Information($"连接耗时: {stopwatch.ElapsedMilliseconds} 毫秒");

                        if (success) {
                            try {
                                // 获取目录中的所有文件
                                // bool exist = FileOperation.IsFileExists(path, today, postfix, floor);
                                bool exist = FileOperation.CheckLatestFolderForTodayFiles(path);


                                if (exist) {
                                    results[device.Path] = new ConnectionResult {
                                        Status = ConnectionStatus.Success,
                                        Profile = "正常",
                                        Message = "正常"
                                    };
                                }
                                else {
                                    List<string> message = new List<string>() {
                                        PromptMessage.DEVICE_NOT_PRODUCED,
                                        PromptMessage.LOG_PATH_CHANGE,
                                    };
                                    results[device.Path] = new ConnectionResult {
                                        Status = ConnectionStatus.Failure,
                                        Profile = PromptMessage.DAILY_LOG_NOT_EXIST,
                                        Message = string.Join("，", message),
                                    };
                                }
                            }
                            finally {
                                // 断开共享连接
                                NetOperation.DisconnectShare(path, true);
                                Log.Information($"{device.Line} {device.Code} {path} 断开共享连接");
                            }
                        }
                        else {
                            results[device.Path] = new ConnectionResult {
                                Status = ConnectionStatus.Failure,
                                Profile = PromptMessage.CONNECTION_TIMED_OUT,
                                Message = "网络异常",
                            };
                        }
                    }
                    //else {
                    //    List<string> message = new List<string>() {
                    //        PromptMessage.DEVICE_SHUT_DOWN,
                    //        PromptMessage.ETHERNET_CABLE_PULLED_OUT,
                    //        PromptMessage.IP_CHANGE,
                    //    };
                    //    
                    //    results[device.Path] = new ConnectionResult { Status = ConnectionStatus.Failure, Profile = PromptMessage.ACCESS_PATH_FAILURE, Message = string.Join('，', message) };
                    //}
                }
                catch (Win32Exception ex) { // 使用device.Path而不是局部变量path，是因为要基于源路径对字典进行搜索，印刷机的path是动态拼接的
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
            Log.Information("当前共享连接任务完成");

            return results;
        }
    }
}