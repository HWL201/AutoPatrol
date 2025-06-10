using System.Diagnostics;
using System.Text.RegularExpressions;

namespace AutoPatrol.Utility
{
    public class DeviceClassify
    {
        // 机况类型驱动
        private static readonly HashSet<string> conditionDriver = new HashSet<string>(StringComparer.Ordinal) {
            "CQ.IOT.SiemensPLCDriver.dll",
            "CQ.IOT.LightDriver.dll",
        };

        // 数据类型驱动
        private static readonly HashSet<string> dataDriver = new HashSet<string>(StringComparer.Ordinal) {
            "CQ.IOT.HT.TRIAOIDriver.dll",
            "CQ.IOT.HT.GluingBZDriver.dll",
            "CQ.IOT.HT.ReflowDriver.dll",
            "CQ.IOT.HT.GarberryGluingDriver.dll",
            "CQ.IOT.HT.LaminatorDriver.dll",
            "CQ.IOT.HT.BorudaLMJDriver.dll",
            "CQ.IOT.HT.PanasonicPrintingDriver.dll",
            "CQ.IOT.HT.SPIDriver.dll",
            "CQ.IOT.HT.TestLXDriver.dll",
            "CQ.IOT.HT.BindDriver.dll",
            "CQ.IOT.HT.TJLMJDriver.dll",
            "CQ.IOT.HT.BorudaQFBJDriver.dll",
            "CQ.IOT.HT.XZPlasmaDriver.dll",
            "CQ.IOT.HT.DispenserDriver.dll",
            "CQ.IOT.HT.BriquettingDriver.dll",
            "CQ.IOT.HT.ClgxAVI2Driver.dll",
            "CQ.IOT.HT.LXTestNewDriver.dll",
            "CQ.IOT.HT.OSLMJDriver.dll",
            "CQ.IOT.HT.LaminatingDriver.dll",
            "CQ.IOT.HT.PunchDriver.dll",
            "CQ.IOT.HT.TestMachinegDriver.dll",
            "CQ.IOT.HT.XRayDriver.dll",
            "CQ.IOT.HT.DZLaserDriver.dll",
            "CQ.IOT.HT.JLBZJDriver.dll",
            "CQ.IOT.HT.LaserDriver.dll",
            "CQ.IOT.HT.BriquettingTestDriver.dll",
            "CQ.IOT.HT.DryingOvenDriver.dll",
            "CQ.IOT.HT.LeaderPunchDriver.dll",
            "CQ.IOT.HT.PlasmaDriver.dll",
        };

        // 特殊设备
        private static readonly HashSet<string> specialDevice = new HashSet<string>(StringComparer.Ordinal) {
            "YHJ1003",
            "YHJ1011",
            "YHJ1020",
            "YHJ1096",
            "YHJ2009",
            "YHJ2217",
            "YHJ2218",
            "YHJ2219",
            "YHJ2225",
            "YHJ2226",
            "YHJ2227",
            "YHJ2228",
            "YHJ2231",
            "YHJ2232",
            "YHJ2233",
            "YHJ2234",
            "YHJ2235",
            "YHJ2236",
            "YHJ2237",
            "YHJ2247",
            "YHJ2248",
            "YHJ2249",

            "X-Ray1002",
            "X-Ray1005",
            "X-Ray1007",

            "HHL1004",
            "HHL1005",
            "HHL1009",
            "HHL1023",

            "TJJ6010",
            "TJJ6011",
        };

        /// <summary>
        /// 判断机况类型
        /// </summary>
        /// <param name="driverName"></param>
        /// <returns></returns>
        public static string TypeJudge(string driverName) {
            if (string.IsNullOrEmpty(driverName)) return "";

            return conditionDriver.Contains(driverName) ? "机况"
                 : dataDriver.Contains(driverName) ? "数据"
                 : "其他";
        }

        /// <summary>
        /// 判断是否为特殊设备
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public static bool YHJJudge(string code) {
            if (string.IsNullOrEmpty(code)) return false;

            return specialDevice.Contains(code) ? true : false;
        }
    }
}
