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
using Serilog;

namespace AutoPatrol.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        private readonly IWebHostEnvironment _webHostEnvironment;

        private readonly MQServer _mqServer;

        public HomeController(ILogger<HomeController> logger, IWebHostEnvironment webHostEnvironment, MQServer mqServer) {
            _logger = logger;

            _webHostEnvironment = webHostEnvironment;

            _mqServer = mqServer;
        }

        public IActionResult Index() {
            return View();
        }

        /// <summary>
        /// 获取进行中的巡检任务
        /// </summary>
        /// <returns></returns>
        public IActionResult GetTaskName() {
            string taskName = "暂无巡检任务进行中";
            bool enable = true;
            if (TaskCache.GetTaskCount() != 0) {
                foreach (var task in TaskCache.GetAllTasks()) {
                    if(task.Value == "手动巡检") {
                        taskName = "当前巡检任务： " + task.Key;
                        enable = false;
                        break;
                    }
                }
            }

            return Ok(new { taskName, enable });
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

                // await DevicePatrol.Patrol(deviceList, filePath);
                // 启动后台任务，不等待结果
                _ = Task.Run(async () => {
                    try {
                        string taskName = Path.GetFileNameWithoutExtension(filePath);
                        TaskCache.TryAddTask(taskName, "手动巡检"); // 添加任务到缓存
                        await DevicePatrol.Patrol(deviceList, filePath);
                    }
                    catch (Exception ex) {
                        Log.Error(ex, "后台巡检任务失败");
                    }
                    finally {
                        TaskCache.RemoveTask(Path.GetFileNameWithoutExtension(filePath)); // 移除任务
                    }
                });

                return Ok(new { code = 200, message = "巡检任务进行中，请稍后片刻" });
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


        public async Task<IActionResult> ThrowUpData([FromBody] List<ResultViewModel> resultList) {
            if (!ModelState.IsValid) {
                return BadRequest(new { code = 400, message = "数据模型匹配失败" });
            }
            if (resultList == null || resultList.Count == 0) {
                return BadRequest(new { code = 400, message = "无可用巡检信息" });
            }

            try {
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

                return Ok(new { status = 200, message = "上抛成功" });
            }
            catch (Exception ex) {
                return StatusCode(500, new { status = 500, message = ex.Message });
            }
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error() {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
