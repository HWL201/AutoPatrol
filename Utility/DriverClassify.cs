using System.Diagnostics;
using System.Text.RegularExpressions;

namespace AutoPatrol.Utility
{
    public class DriverClassify {
        // 机况驱动
        private static readonly HashSet<string> conditionDriver = new HashSet<string>(StringComparer.Ordinal) {
            "CQ.IOT.SiemensPLCDriver.dll",
            "CQ.IOT.LightDriver.dll",
        };

        // 数据驱动
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

        public static string TypeJudge(string driverName) {
            if (string.IsNullOrEmpty(driverName)) return "";

            return conditionDriver.Contains(driverName) ? "机况"
                 : dataDriver.Contains(driverName) ? "数据"
                 : "其他";
        }
    }
}
