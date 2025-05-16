using AutoPatrol.Models;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

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
        /// 更新设备配置
        /// </summary>
        /// <param name="deviceList"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> UpdateConfiguration([FromBody] List<DeviceViewModel> deviceList) {
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
    }
}
