using AutoPatrol.Models;
using AutoPatrol.Utility;
using Microsoft.AspNetCore.Hosting;
using Newtonsoft.Json;

namespace AutoPatrol.Services
{
    public class TimerService : ITimerService
    {
        private readonly IWebHostEnvironment _webHostEnvironment;

        public TimerService(IWebHostEnvironment webHostEnvironment) {
            _webHostEnvironment = webHostEnvironment;
        }

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

                await DevicePatrol.Patrol(deviceList, filePath);

                await Task.CompletedTask;
            }
            catch (Exception ex) {
                Console.WriteLine($"执行定时巡检任务时发生错误：{ex.Message}");
            }
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
