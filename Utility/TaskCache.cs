using System.Collections.Concurrent;

namespace AutoPatrol.Utility
{
    public class TaskCache
    {
        // 使用ConcurrentDictionary存储任务名称，值为bool类型
        private static readonly ConcurrentDictionary<string, string> _taskNames = new ConcurrentDictionary<string, string>();

        /// <summary>
        /// 尝试添加任务名称到缓存
        /// </summary>
        /// <param name="taskName">任务名</param>
        /// <param name="patrolWay">巡检方式</param>
        /// <returns></returns>
        public static bool TryAddTask(string taskName, string patrolWay) {
            return _taskNames.TryAdd(taskName, patrolWay);
        }

        /// <summary>
        /// 从缓存中移除任务名称
        /// </summary>
        /// <param name="taskName">任务名称</param>
        public static void RemoveTask(string taskName) {
            _taskNames.TryRemove(taskName, out _);
        }

        /// <summary>
        /// 检查任务名称是否存在于缓存中
        /// </summary>
        /// <param name="taskName">任务名称</param>
        /// <returns>如果存在返回true，否则返回false</returns>
        public static bool ContainsTask(string taskName) {
            return _taskNames.ContainsKey(taskName);
        }

        /// <summary>
        /// 获取当前缓存中的任务数量
        /// </summary>
        /// <returns>任务数量</returns>
        public static int GetTaskCount() {
            return _taskNames.Count;
        }

        /// <summary>
        /// 获取当前缓存中的所有任务
        /// </summary>
        /// <returns>任务名称列表</returns>
        public static List<KeyValuePair<string, string>> GetAllTasks() {
            return _taskNames.ToList();
        }

        /// <summary>
        /// 获取当前缓存中的第一个任务名称
        /// </summary>
        /// <returns>任务名称列表</returns>
        public static string GetFirstTaskName() {
            return _taskNames.Keys.FirstOrDefault();
        }
    }
}

