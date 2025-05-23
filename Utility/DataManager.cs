using AutoPatrol.Models;

namespace AutoPatrol.Utility
{
    public class DataManager
    {
        // Dictionary<Line, Dictionary<Code, Dictionary<Item, ResultViewModel>>>
        private static Dictionary<string, Dictionary<string, Dictionary<string, ResultViewModel>>> dataStore
            = new Dictionary<string, Dictionary<string, Dictionary<string, ResultViewModel>>>(StringComparer.OrdinalIgnoreCase);


        // 添加记录
        public static void AddRecord(ResultViewModel record) {
            // 初始化Line层级
            if (!dataStore.TryGetValue(record.Line, out var codeDict)) {
                codeDict = new Dictionary<string, Dictionary<string, ResultViewModel>>(StringComparer.OrdinalIgnoreCase);
                dataStore[record.Line] = codeDict;
            }

            // 初始化Code层级
            if (!codeDict.TryGetValue(record.Code, out var itemDict)) {
                itemDict = new Dictionary<string, ResultViewModel>(StringComparer.OrdinalIgnoreCase);
                codeDict[record.Code] = itemDict;
            }

            // 添加或更新记录
            itemDict[record.Item] = record;
        }


        // 按Line, Code, Item三级索引快速查询
        public static ResultViewModel GetRecord(string line, string code, string item) {
            if (dataStore.TryGetValue(line, out var codeDict) &&
                codeDict.TryGetValue(code, out var itemDict) &&
                itemDict.TryGetValue(item, out var record)) {
                return record;
            }
            return null;
        }


        // 查询某产线下的所有记录
        public static List<ResultViewModel> GetRecordsByLine(string line) {
            if (!dataStore.TryGetValue(line, out var codeDict))
                return new List<ResultViewModel>();

            return codeDict.Values.SelectMany(itemDict => itemDict.Values).ToList();
        }
    }
}
