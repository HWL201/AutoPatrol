using AutoPatrol.Models;
using System.Net;
using System.Net.NetworkInformation;

namespace AutoPatrol.Utility
{
    public static class DevicePatrol
    {
        /// <summary>
        /// 异步Ping IP 地址集合并分别返回结果
        /// </summary>
        /// <param name="ipList">IP地址集合</param>
        /// <returns></returns>
        public static async Task<Dictionary<string, bool>> PingIPList(List<string> ipList) {
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
                Console.WriteLine(result.IP + result.Success);
            }

            return pingResults;
        }

        #region 废弃代码

        /// <summary>
        /// Ping IP 地址并返回结果
        /// </summary>
        /// <param name="ip">IP地址</param>
        /// <returns></returns>
        public static async Task<bool> PingIP(string ip) {
            if (string.IsNullOrEmpty(ip)) {
                return false;
            }

            try {
                using (Ping ping = new Ping()) {
                    PingReply reply = await ping.SendPingAsync(ip);
                    return reply.Status == IPStatus.Success;
                }
            }
            catch {
                return false;
            }
        }

        /// <summary>
        /// Ping IP 地址集合并分别返回结果
        /// </summary>
        /// <param name="ipList">IP地址集合</param>
        /// <returns></returns>
        public static async IAsyncEnumerable<bool> PingIPList2(List<string> ipList) {
            if (ipList == null || ipList.Count == 0) {
                yield break;
            }

            using (Ping ping = new Ping()) {
                bool result;
                foreach (var ip in ipList) {
                    try {
                        PingReply reply = await ping.SendPingAsync(ip, 500);
                        result = reply.Status == IPStatus.Success;
                    }
                    catch {
                        // 单个IP ping失败，返回false但继续处理其他IP
                        result = false;
                    }
                    yield return result;
                }
            }
        }

        #endregion
    }
}
