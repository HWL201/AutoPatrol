using AutoPatrol.Models;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Net.Http.Json;
using System.Text.RegularExpressions;

namespace AutoPatrol.Controllers
{
    public class ConfigController : Controller
    {
        private readonly IWebHostEnvironment _webHostEnvironment;

        public ConfigController(IWebHostEnvironment webHostEnvironment) {
            _webHostEnvironment = webHostEnvironment;
        }

        public IActionResult Configuration() {
            return View();
        }

        /// <summary>
        /// 获取设备配置
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> GetConfiguration() {
            try {
                var filePath = Path.Combine(_webHostEnvironment.ContentRootPath, "Config", "DeviceInfoConfig.json");

                if (!System.IO.File.Exists(filePath)) {
                    return NotFound(new { code = 404, message = "配置文件不存在" });
                }

                var json = await System.IO.File.ReadAllTextAsync(filePath);
                var deviceList = JsonConvert.DeserializeObject<List<DeviceViewModel>>(json);

                return Ok(new { code = 200, message = "加载成功", data = deviceList });
            }
            catch (Exception ex) {
                return StatusCode(500, new {
                    code = 500,
                    message = "获取配置失败",
                    error = ex.Message
                });
            }
        }


        /// <summary>
        /// 更新设备模型配置
        /// </summary>
        /// <param name="deviceList"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> UpdateModelConfiguration([FromBody] List<DeviceViewModel> deviceList) {
            if (!ModelState.IsValid) {
                return BadRequest(new { code = 400, message = "数据模型匹配失败" });
            }

            var json = JsonConvert.SerializeObject(deviceList, Formatting.Indented);
            var filePath = Path.Combine(_webHostEnvironment.ContentRootPath, "Config", "DeviceInfoConfig.json");

            // 确保目录存在
            var directory = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(directory)) {
                Directory.CreateDirectory(directory);
            }

            // 写入文件
            await System.IO.File.WriteAllTextAsync(filePath, json);
            return Ok(new { code = 200, message = "保存成功" });
        }


        /// <summary>
        /// 获取更新定时任务配置
        /// </summary>
        /// <param name="timers"></param>
        /// <returns></returns>
        public async Task<IActionResult> GetTimerConfiguration() {
            try {
                var filePath = Path.Combine(_webHostEnvironment.ContentRootPath, "Config", "TimerConfig.json");

                if (!System.IO.File.Exists(filePath)) {
                    return NotFound(new { code = 404, message = "配置文件不存在" });
                }

                var json = await System.IO.File.ReadAllTextAsync(filePath);
                var timerList = JsonConvert.DeserializeObject<List<TimerViewModel>>(json);

                return Ok(new { code = 200, message = "加载成功", data = timerList });
            }
            catch (Exception ex) {
                return StatusCode(500, new {
                    code = 500,
                    message = "获取配置失败",
                    error = ex.Message
                });
            }
        }


        /// <summary>
        /// 更新定时任务配置
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        public IActionResult UpdateTimerConfiguration([FromBody] List<TimerViewModel> timers) {
            if (!ModelState.IsValid) {
                return BadRequest(new { status = 400, message = "数据模型匹配失败" });
            }

            if (timers.Any(t => !Regex.IsMatch(t.Time, @"^\d{2}:\d{2}:\d{2}$"))) {
                return BadRequest(new { status = 400, message = "时间格式必须为HH:MM:SS" });
            }

            if (timers.GroupBy(t => t.Time).Any(g => g.Count() > 1)) {
                return BadRequest(new { status = 400, message = "存在重复的时间节点" });
            }

            try {
                var json = JsonConvert.SerializeObject(timers, Formatting.Indented);

                var filePath = Path.Combine(_webHostEnvironment.ContentRootPath, "Config", "TimerConfig.json");
                var directory = Path.GetDirectoryName(filePath);
                Directory.CreateDirectory(directory);
                System.IO.File.WriteAllText(filePath, json);

                return Ok(new { status = 200, message = "保存成功" });
            }
            catch (Exception ex) {
                return StatusCode(500, new { status = 500, message = $"未知错误：{ex.Message}" });
            }
        }


        public IActionResult Copy() {
            return View();
        }


        /// <summary>
        /// 获取文件复制配置
        /// </summary>
        /// <param name="timers"></param>
        /// <returns></returns>
        public async Task<IActionResult> GetCopyConfiguration() {
            try {
                var filePath = Path.Combine(_webHostEnvironment.ContentRootPath, "Config", "FileCopyConfig.json");

                if (!System.IO.File.Exists(filePath)) { // 如果文件不存在
                    var directory = Path.GetDirectoryName(filePath);
                    Directory.CreateDirectory(directory);   // 如果目录不存在
                    using (System.IO.File.Create(filePath)) { }
                }

                var json = await System.IO.File.ReadAllTextAsync(filePath);
                var timerList = JsonConvert.DeserializeObject<CopyViewModel>(json);

                return Ok(new { code = 200, message = "加载成功", data = timerList });
            }
            catch (Exception ex) {
                return StatusCode(500, new {
                    code = 500,
                    message = "获取配置失败",
                    error = ex.Message
                });
            }
        }


        /// <summary>
        /// 更新文件复制配置
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> UpdateCopyConfiguration([FromBody] CopyViewModel data) {
            if (!ModelState.IsValid) {
                return BadRequest(new { status = 400, message = "数据模型匹配失败" });
            }

            var json = JsonConvert.SerializeObject(data, Formatting.Indented);

            var filePath = Path.Combine(_webHostEnvironment.ContentRootPath, "Config", "FileCopyConfig.json");
            var directory = Path.GetDirectoryName(filePath);
            Directory.CreateDirectory(directory);
            await System.IO.File.WriteAllTextAsync(filePath, json);

            return Ok(new { status = 200, message = "保存成功" });
        }
    }
}
