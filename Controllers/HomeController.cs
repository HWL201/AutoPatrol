using System;
using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using AutoPatrol.Asserts;
using AutoPatrol.Models;
using AutoPatrol.Utility;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using OfficeOpenXml;

namespace AutoPatrol.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        private readonly IWebHostEnvironment _webHostEnvironment;

        public HomeController(ILogger<HomeController> logger, IWebHostEnvironment webHostEnvironment) {
            _logger = logger;

            _webHostEnvironment = webHostEnvironment;
        }

        public IActionResult Index() {
            return View();
        }

        /// <summary>
        /// 手动巡检，并将结果写入Excel文件
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> ManualPatrol([FromBody] List<DeviceViewModel> deviceList) {
            if (!ModelState.IsValid) {
                return BadRequest(new { code = 400, message = "数据模型匹配失败" });
            }
            if (deviceList == null || deviceList.Count == 0) {
                return BadRequest(new { code = 400, message = "设备列表为空，请导入设备模板" });
            }

            try {
                string fileName = $"{DateTime.Now.ToString("yyyyMMdd_HHmmss")}手动巡检.xlsx";
                string filePath = Path.Combine(_webHostEnvironment.ContentRootPath, "Record", fileName);

                await DevicePatrol.Patrol(deviceList, filePath);

                ViewBag.FilePath = filePath;

                return Ok(new { code = 200, message = "批量巡检完成，文件已存入" });
            }
            catch (Exception ex) {
                return StatusCode(500, new { code = 500, message = $"服务器异常{ex.Message}" });
            }
        }

        public IActionResult Result() {
            return View();
        }

        /// <summary>
        /// 获取巡检结果
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        [HttpGet]
        public IActionResult GetPatrolResult([FromQuery] string fileName) {
            if (string.IsNullOrEmpty(fileName)) {
                return BadRequest(new { code = 400, message = "文件名不能为空" });
            }
            try {
                string filePath = Path.Combine(_webHostEnvironment.ContentRootPath, "Record", fileName);
                return Ok(new { code = 200, message = "加载成功", data = FileOperation.ReadExcel(filePath) });
            }
            catch (Exception ex) {
                return StatusCode(500, new { code = 500, message = ex.Message });
            }
        }


        /// <summary>
        /// 获取目标文件名集合
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public IActionResult GetTargetFileName([FromQuery] string date) {
            List<string> fileNameList = new List<string>();
            string directory = Path.Combine(_webHostEnvironment.ContentRootPath, "Record");
            // foreach (var fileName in FileOperation.GetFileNameList(directory)) {
            foreach (var fileName in FileOperation.GetFiles(directory, 1).Select(a => a.Name)) {
                if (fileName.StartsWith(date)) {
                    fileNameList.Add(fileName);
                }
            }
            return Ok(new { code = 200, message = "加载成功", data = fileNameList });
        }


        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error() {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
