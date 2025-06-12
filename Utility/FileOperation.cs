using AutoPatrol.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using OfficeOpenXml;
using Serilog;
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
                    if (int.TryParse(worksheet.Cells[row, 10].Value?.ToString(), out int duration)) {
                        rowData.Duration = duration;
                    }
                    else {
                        rowData.Duration = 1;
                    }
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

                Log.Information($"Excel文件已保存至: {filePath}");
            }
            catch (Exception ex) {
                Log.Error(ex, "保存Excel文件时出错");
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

            //foreach (var item in GetDirectorys(filePath, floor)) {
            //    files.AddRange(item.GetFiles());
            //}

            foreach (var item in GetDirectorys(filePath)) {
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


        /// <summary>
        /// 返回路径下所有子目录
        /// </summary>
        /// <param name="rootPath"></param>
        /// <returns></returns>
        /// <exception cref="DirectoryNotFoundException"></exception>
        public static List<DirectoryInfo> GetDirectorys(string rootPath) {
            Directory.CreateDirectory(rootPath);
            //if (!Directory.Exists(rootPath))
            //    throw new DirectoryNotFoundException($"目录不存在: {rootPath}");

            var directories = new List<DirectoryInfo>();
            var stack = new Stack<DirectoryInfo>();
            stack.Push(new DirectoryInfo(rootPath));

            while (stack.Count > 0) {
                var currentDir = stack.Pop();

                try {
                    // 将当前目录添加到结果列表
                    directories.Add(currentDir);

                    // 获取子目录并压入栈
                    foreach (var subDir in currentDir.GetDirectories()) {
                        stack.Push(subDir);
                    }
                }
                catch (UnauthorizedAccessException) {
                    Log.Warning($"无权访问目录: {currentDir.FullName}");
                }
                catch (DirectoryNotFoundException) {
                    Log.Warning($"警告: 目录已不存在 {currentDir.FullName}");
                }
            }
            return directories.OrderBy(d => d.LastWriteTime).ToList();
        }


        /// <summary>
        /// 检查指定根目录下的最新子文件夹中是否存在今天的文件
        /// </summary>
        /// <param name="rootFolderPath">根目录路径</param>
        public static bool CheckLatestFolderForTodayFiles(string rootFolderPath) {
            try {
                // 1. 获取最新的子文件夹
                string latestSubFolder = FindDeepestLatestFolder(rootFolderPath);

                if (latestSubFolder == null) {
                    Log.Information("未找到任何子文件夹！");
                    return false;
                }

                Log.Information($"最新的子文件夹路径: {latestSubFolder}");

                // 2. 检查该文件夹中是否有今天的文件
                bool foundTodayFile = CheckTodayFilesInFolder(latestSubFolder);

                Log.Information(foundTodayFile
                    ? $"{rootFolderPath}存在今天的日志文件！"
                    : $"{rootFolderPath}未找到今天的日志文件。");
                return foundTodayFile;
            }
            catch (Exception ex) {
                Log.Error(ex, "发生错误");
                return false;
            }
        }


        /// <summary>
        /// 逐层查找最新的最深层子文件夹
        /// </summary>
        static string FindDeepestLatestFolder(string rootFolder) {
            string currentFolder = rootFolder;
            string latestSubFolder = null;

            while (true) {
                // 获取当前层级的子文件夹，并按修改时间降序排序
                var subFolders = Directory.GetDirectories(currentFolder)
                    .OrderByDescending(dir => Directory.GetLastWriteTime(dir))
                    .ToList();

                if (subFolders.Count == 0)
                    break; // 没有更多子文件夹，退出循环

                latestSubFolder = subFolders.First(); // 当前层级最新的子文件夹
                currentFolder = latestSubFolder;     // 进入下一层
            }

            return latestSubFolder ?? rootFolder; // 如果没有任何子文件夹，返回根目录
        }


        /// <summary>
        /// 检查文件夹中是否有今天的文件（按文件名或修改时间）
        /// </summary>
        static bool CheckTodayFilesInFolder(string folderPath) {
            string todayDateString = DateTime.Now.ToString("yyyy-MM-dd");
            string todayDateString2 = DateTime.Now.ToString("yyyyMMdd");
            DateTime today = DateTime.Today;

            /*foreach (string file in Directory.GetFiles(folderPath)) {
                // 按文件名匹配（如 log_2023-10-05.txt）
                if (Path.GetFileName(file).Contains(todayDateString)) {
                    Log.Information($"匹配到今天的文件（按文件名）: {Path.GetFileName(file)}");
                    return true;
                }

                // 按文件修改时间匹配
                if (File.GetLastWriteTime(file).Date == today) {
                    Log.Information($"匹配到今天的文件（按修改时间）: {Path.GetFileName(file)}");
                    return true;
                }
            }*/

            foreach (string file in Directory.EnumerateFiles(folderPath)) {
                if (Path.GetFileName(file).Contains(todayDateString)) {
                    Log.Information($"匹配到今天的文件（按文件名）: {Path.GetFileName(file)}");
                    return true;
                }

                if (Path.GetFileName(file).Contains(todayDateString2)) {
                    Log.Information($"匹配到今天的文件（按文件名）: {Path.GetFileName(file)}");
                    return true;
                }

                // 按文件修改时间匹配
                if (File.GetLastWriteTime(file).Date == today) {
                    Log.Information($"匹配到今天的文件（按修改时间）: {Path.GetFileName(file)}");
                    return true;
                }
            }

            return false;
        }


        /// <summary>
        /// 异步文件拷贝
        /// </summary>
        /// <param name="sourceFile"></param>
        /// <param name="targetFile"></param>
        /// <returns></returns>
        public static async Task CopyFileAsync(string sourceFile, string targetFile) {
            if (!File.Exists(sourceFile)) {
                return;
            }

            // 确保父目录存在
            var directory = Path.GetDirectoryName(targetFile);
            Directory.CreateDirectory(directory);

            using (var sourceStream = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4 * 1024 * 1024, FileOptions.Asynchronous)) {
                using (var targetStream = new FileStream(targetFile, FileMode.Create, FileAccess.Write, FileShare.ReadWrite, 4 * 1024 *1024, FileOptions.Asynchronous)) {
                    // stream.CopyTo(targetStream);
                    var buffer = new byte[4 * 1024 * 1024];
                    long totalBytesRead = 0;
                    long fileLength = sourceStream.Length;
                    int bytesRead;

                    while((bytesRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length)) > 0) {
                        await targetStream.WriteAsync(buffer, 0, bytesRead);
                        totalBytesRead += bytesRead;
                        // 可以在这里添加进度更新逻辑
                        Log.Information($"已复制 {totalBytesRead} 字节，进度: {((double)totalBytesRead / fileLength * 100):F2}%");
                    }

                    await targetStream.FlushAsync(); // 确保所有数据都写入磁盘
                }
            }
        }
    }
}
