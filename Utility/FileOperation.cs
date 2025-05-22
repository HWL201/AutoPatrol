using AutoPatrol.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using OfficeOpenXml;
using System;
using System.IO;
using System.Linq;

namespace AutoPatrol.Utility
{
    public static class FileOperation
    {
        /// <summary>
        /// 读取 xlsx 文件并转换为 ResultViewModel 列表
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public static List<ResultViewModel> ReadExcel(string filePath) {
            var result = new List<ResultViewModel>();

            if (!File.Exists(filePath)) {
                return result;
            }

            // 设置许可证（非商业用途可免费使用）
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length == 0) {
                return result;
            }

            using (var package = new ExcelPackage(new FileInfo(filePath))) {
                // 获取第一个工作表
                var worksheet = package.Workbook.Worksheets[0];

                // 获取最大行数和列数
                int rowCount = worksheet.Dimension.Rows;
                int colCount = worksheet.Dimension.Columns;

                // 从第二行开始读取数据
                for (int row = 2; row <= rowCount; row++) {
                    var rowData = new ResultViewModel();
                    rowData.Line = worksheet.Cells[row, 1].Value.ToString();
                    if (int.TryParse(worksheet.Cells[row, 2].Value?.ToString(), out int num)) {
                        rowData.Num = num;
                    }
                    else {
                        rowData.Num = 0;
                    }
                    rowData.DeviceType = worksheet.Cells[row, 3].Value.ToString();
                    rowData.Code = worksheet.Cells[row, 4].Value.ToString();
                    rowData.Ip = worksheet.Cells[row, 5].Value.ToString();
                    rowData.Item = worksheet.Cells[row, 6].Value.ToString();
                    rowData.Result = worksheet.Cells[row, 7].Value.ToString();
                    rowData.Describe = worksheet.Cells[row, 8].Value.ToString();
                    rowData.Message = worksheet.Cells[row, 9].Value.ToString();
                    result.Add(rowData);
                }
            }

            return result;
        }


        /// <summary>
        /// 将数据流写进 xlsx 文件中
        /// </summary>
        /// <param name="stream">数据流</param>
        /// <param name="filePath">文件路径</param>
        public static void WriteExcel(Stream stream, string filePath) {
            // 确保目录存在
            var directory = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(directory)) {
                Directory.CreateDirectory(directory);
            }

            try {
                using (FileStream fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write)) {
                    stream.Position = 0; // 重置流位置到开始
                    stream.CopyTo(fileStream); // 复制到文件流
                }

                Console.WriteLine($"Excel文件已保存至: {filePath}");
            }
            catch (Exception ex) {
                Console.WriteLine($"保存Excel文件时出错: {ex.Message}");
                // 可以考虑记录日志或抛出更具体的异常
            }
        }


        /// <summary>
        /// 判断指定时间，指定后缀的文件是否存在
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="floor"></param>
        /// <returns></returns>
        public static bool IsFileExists(string filePath, DateTime date, string postfix = "", int floor = 1) {
            List<FileInfo> files = GetFiles(filePath, floor);

            if (files.Count == 0) {
                return false;
            }
            if (!string.IsNullOrEmpty(postfix)) {
                files = files.Where(f => f.Extension.ToUpper() == postfix.ToUpper() && f.LastWriteTime.Date == date).ToList();
            }

            return files.Count > 0;
        }


        /// <summary>
        /// 获取指定目录下文件
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="floor"></param>
        /// <returns></returns>
        public static List<FileInfo> GetFiles(string filePath, int floor) {
            List<FileInfo> files = new List<FileInfo>();

            foreach (var item in GetDirectorys(filePath, floor)) {
                files.AddRange(item.GetFiles());
            }

            return files.OrderBy(a => a.LastWriteTime).ToList();
        }


        /// <summary>
        /// 获取共享路径指定层级下所有子目录
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public static List<DirectoryInfo> GetDirectorys(string filePath, int floor) {
            List<DirectoryInfo> directorys = new List<DirectoryInfo>();

            directorys.Add(new DirectoryInfo(filePath));

            for (int i = 0; i < floor - 1; i++) {
                List<DirectoryInfo> tempList = new List<DirectoryInfo>();
                foreach (var directory in directorys) {
                    tempList.AddRange(directory.GetDirectories());
                }
                directorys = tempList;
            }

            return directorys;
        }


        #region 废弃代码

        /// <summary>
        /// 通用 xlsx 文件读取方法
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns>文件内容</returns>
        private static List<Dictionary<string, object>> CommonReadExcel(string filePath) {
            var result = new List<Dictionary<string, object>>();

            // 设置许可证（非商业用途可免费使用）
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using (var package = new ExcelPackage(new FileInfo(filePath))) {
                // 获取第一个工作表
                var worksheet = package.Workbook.Worksheets[0];

                // 获取最大行数和列数
                int rowCount = worksheet.Dimension.Rows;
                int colCount = worksheet.Dimension.Columns;

                // 获取表头（第一行）
                var headers = new string[colCount];
                for (int col = 1; col <= colCount; col++) {
                    headers[col - 1] = worksheet.Cells[1, col].Value?.ToString() ?? $"Column{col}";
                }

                // 从第二行开始读取数据
                for (int row = 2; row <= rowCount; row++) {
                    var rowData = new Dictionary<string, object>();
                    for (int col = 1; col <= colCount; col++) {
                        rowData[headers[col - 1]] = worksheet.Cells[row, col].Value;
                    }
                    result.Add(rowData);
                }
            }

            return result;
        }

        /* 错误代码
        * public static void WriteExcel(string filePath, List<Dictionary<string, object>> data) {
           // 设置许可证（非商业用途可免费使用）
           ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
           using (var package = new ExcelPackage()) {
               var worksheet = package.Workbook.Worksheets.Add("Sheet1");
               // 写入表头
               var headers = data.First().Keys.ToList();
               for (int col = 0; col < headers.Count; col++) {
                   worksheet.Cells[1, col + 1].Value = headers[col];
               }
               // 写入数据
               for (int row = 0; row < data.Count; row++) {
                   for (int col = 0; col < headers.Count; col++) {
                       worksheet.Cells[row + 2, col + 1].Value = data[row][headers[col]];
                   }
               }
               // 保存文件
               package.SaveAs(new FileInfo(filePath));
           }
       }*/

        #endregion
    }
}
