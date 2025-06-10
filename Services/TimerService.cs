using AutoPatrol.Models;
using AutoPatrol.Utility;
using Microsoft.AspNetCore.Hosting;
using Newtonsoft.Json;
using Serilog;
using System.Diagnostics;

namespace AutoPatrol.Services
{
    public class TimerService : ITimerService
    {
        private readonly IWebHostEnvironment _webHostEnvironment;

        private readonly MQServer _mqServer;


        public TimerService(IWebHostEnvironment webHostEnvironment, MQServer mqServer) {
            _webHostEnvironment = webHostEnvironment;

            _mqServer = mqServer;
        }


        /// <summary>
        /// 执行定时巡检和上抛任务
        /// </summary>
        /// <returns></returns>
        public async Task ExecuteScheduledTaskAsync() {
            try {
                var configFilePath = Path.Combine(_webHostEnvironment.ContentRootPath, "Config", "DeviceInfoConfig.json");

                var directory = Path.GetDirectoryName(configFilePath);

                if (!Directory.Exists(directory)) {
                    Directory.CreateDirectory(directory);
                }

                if (!File.Exists(configFilePath)) {
                    using (File.Create(configFilePath)) { }
                }

                var json = await File.ReadAllTextAsync(configFilePath);
                var deviceList = JsonConvert.DeserializeObject<List<DeviceViewModel>>(json);

                string fileName = $"{DateTime.Now.ToString("yyyyMMdd")}自动巡检{GetShiftName()}.xlsx";
                string filePath = Path.Combine(_webHostEnvironment.ContentRootPath, "Record", fileName);

                var resultList = await DevicePatrol.Patrol(deviceList, filePath);

                await _mqServer.InitializeAsync();

                foreach (var result in resultList) {
                    await _mqServer.SendMesDataAsync(new {
                        trx_name = "eqp_data",
                        rpt_time = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fff"),
                        box_code = result.Code,
                        msg_id = DateTime.Now.ToString("yyyyMMddHHmmssfff"),
                        data = new {
                            eqp_code = result.Code,
                            product_code = "",
                            @params = new List<dynamic>() {
                                new {
                                    k = "STATUS_MSG",
                                    v = result.Describe,
                                }
                            }
                        }
                    });
                }

                _mqServer.CloseConnection();

                await Task.CompletedTask;
            }
            catch (Exception ex) {
                Log.Error(ex, "执行定时巡检任务时发生错误");
            }
        }


        /// <summary>
        /// 执行定时复制任务
        /// </summary>
        /// <returns></returns>
        public async Task ExecuteFileCopyTaskAsync() {
            string configFilePath = Path.Combine(_webHostEnvironment.ContentRootPath, "Config", "FileCopyConfig.json");

            if (!File.Exists(configFilePath)) {
                Log.Warning("FileCopyConfig.json不存在");
                return;
            }

            var json = await File.ReadAllTextAsync(configFilePath);
            var deviceList = JsonConvert.DeserializeObject<CopyViewModel>(json);

            if (deviceList?.Tasks.Count == 0) {
                Log.Warning("未检测到复制任务");
            }

            var semaphore = new SemaphoreSlim(4);
            var tasks = new List<Task>();

            foreach (var device in deviceList.Tasks) {
                try {
                    await semaphore.WaitAsync();

                    if (string.IsNullOrEmpty(device.SourceFile) || string.IsNullOrEmpty(device.TargetFile)) {
                        Log.Warning($"设备 {device.Code} 的源文件或目标文件未配置，跳过该任务");
                        continue;
                    }
                    if (string.IsNullOrEmpty(device.Account)) {
                        Log.Warning($"设备 {device.Code} 的账号未配置，跳过该任务");
                        continue;
                    }

                    string directory = Path.GetDirectoryName(device.SourceFile);
                    bool success = await NetOperation.ConnectToShareAsync(directory, device.Account, device.Password);

                    if (success) {
                        Log.Information($"设备 {device.Code} 连接共享路径成功，开始复制文件");
                        // 启动复制任务
                        tasks.Add(Task.Run(async () => {
                            try {
                                var stopwatch = new Stopwatch();
                                stopwatch.Start();
                                await FileOperation.CopyFileAsync(device.SourceFile, device.TargetFile);
                                stopwatch.Stop();
                                Log.Information($"设备 {device.Code} 文件复制成功，耗时{stopwatch.ElapsedMilliseconds}毫秒");
                            }
                            catch (Exception ex) {
                                Log.Error(ex, $"设备 {device.Code} 文件复制发生错误");
                            }
                            finally {
                                NetOperation.DisconnectShare(directory);
                                Log.Information($"设备 {device.Code} 断开共享连接");
                            }
                        }));
                    }
                    else {
                        Log.Error($"设备 {device.Code} 连接失败，无法进行文件复制");
                        semaphore.Release();
                    }
                }
                catch (Exception ex) {
                    Log.Error(ex, "执行文件复制任务时发生错误");
                }
                finally {
                    semaphore.Release();
                }
            }

            // 等待所有连接任务完成
            await Task.WhenAll(tasks);
        }

        private string GetShiftName() {
            int hour = DateTime.Now.Hour;

            if (hour == 8) {
                return "早班";
            }
            else if (hour == 20) {
                return "晚班";
            }
            else {
                return $"未知班次_{DateTime.Now.ToString("HHmmss")}";
            }
        }
    }
}
