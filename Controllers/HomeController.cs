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

                    var pingResult = await DevicePatrol.PingIPList(deviceList.Select(a => a.Ip).ToList());

                    foreach (var device in deviceList) {
                        string? result;
                        string? describe;
                        bool isPingSuccess;
                        List<string> message = new List<string>();
                        if (pingResult.TryGetValue(device.Ip, out isPingSuccess) && isPingSuccess) {
                            result = "成功";
                            describe = "无异常";
                            message.Add("");
                        }
                        else {
                            result = "失败";
                            describe = "IP无法PING通";
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
                        worksheet.Cells[rowIndex, 9].Value = string.Join("，", message);
                        rowIndex++;
                    }
                    package.Save();
                }

                string fileName = $"{DateTime.Now.ToString("yyyyMMdd_HHmmss")}手动巡检.xlsx";
                string filePath = Path.Combine(_webHostEnvironment.ContentRootPath, "Record", fileName);
                FileOperation.WriteExcel(stream, filePath);

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
            foreach (var fileName in FileOperation.GetFileNameList(directory)) {
                if(fileName.StartsWith(date)) {
                    fileNameList.Add(fileName);
                    Console.WriteLine(fileName);
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
