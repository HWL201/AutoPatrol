using AutoPatrol.Models;
using AutoPatrol.Services;
using Newtonsoft.Json;
using Serilog;
using System.Globalization;

public class FixedTimeScheduler : BackgroundService
{
    private readonly Dictionary<string, DateTime> _lastFileChangeTime = new(StringComparer.OrdinalIgnoreCase); // 用于防抖动处理，记录上次文件变更时间
    private readonly object _fileChangeLock = new object();     // 用于文件变更的线程安全锁
    private readonly TimeSpan _debounceTime = TimeSpan.FromSeconds(1); // 防抖动时间间隔，1秒

    private List<TimeSpan> _patrolExecutionTimes;   // 定时巡检执行时间集合
    private DateTime _copyExecutionTime;            // 定时复制执行时间
    private int _cycle;                             // 定时复制执行周期

    private readonly IServiceProvider _serviceProvider;
    private readonly IWebHostEnvironment _env;
    private readonly FileSystemWatcher _watcher;
    private readonly SemaphoreSlim _reloadLock = new SemaphoreSlim(1, 1);
    private CancellationTokenSource _delayCancellationTokenSource = new CancellationTokenSource();  // 用于取消延迟的令牌源
    private readonly Dictionary<string, Action<FileSystemEventArgs>> _fileCallbacks = new(StringComparer.OrdinalIgnoreCase);    // 不同文件类型的回调字典

    public FixedTimeScheduler(IServiceProvider serviceProvider, IWebHostEnvironment env, ILogger<FixedTimeScheduler> logger) {
        _serviceProvider = serviceProvider;

        _env = env;

        // 初始化文件监听回调
        RegisterFileCallbackInit();

        // 初始化执行时间
        _patrolExecutionTimes = LoadPatrolExecutionTimes();
        (_copyExecutionTime, _cycle) = LoadCopyExecutionTime();

        // 初始化文件监听
        var configPath = Path.Combine(_env.ContentRootPath, "Config", "TimerConfig.json");
        var directory = Path.GetDirectoryName(configPath);
        var fileName = Path.GetFileName(configPath);

        _watcher = new FileSystemWatcher(directory, "*.*") {
            EnableRaisingEvents = true,
            NotifyFilter = NotifyFilters.LastWrite
        };

        _watcher.Changed += OnConfigFileChanged;
    }


    /// <summary>
    /// 文件监听器变更事件的管理函数，负责调用对应回调函数
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnConfigFileChanged(object sender, FileSystemEventArgs e) {
        string fileName = Path.GetFileName(e.Name);

        // 防抖动处理，避免频繁触发同一文件的变更事件
        if (!DebounceFileChange(fileName)) {
            return;
        }

        // 在字典中查找该文件名对应的回调函数
        if (_fileCallbacks.TryGetValue(fileName, out var callback)) {
            callback(e);
        }
    }


    /// <summary>
    /// 文件变更防抖动处理
    /// </summary>
    /// <returns></returns>
    private bool DebounceFileChange(string fileName) {
        lock (_fileChangeLock) {
            DateTime now = DateTime.Now;

            // 检查是否存在该文件的上次变更时间
            if (_lastFileChangeTime.TryGetValue(fileName, out DateTime lastChangeTime)) {
                if (now - lastChangeTime < _debounceTime) {
                    return false;
                }
            }

            _lastFileChangeTime[fileName] = now; // 更新上次变更时间
            return true;
        }
    }


    /// <summary>
    /// 重载巡检配置（文件变更时执行）
    /// </summary>
    /// <returns></returns>
    private async Task ReloadPatrolConfigAsync() {
        // 防止多个变更同时触发
        if (!_reloadLock.Wait(0))
            return;

        try {
            // 等待文件写入完成
            await Task.Delay(500);

            var newTimes = LoadPatrolExecutionTimes();
            if (newTimes != null && newTimes.Any()) {
                _patrolExecutionTimes = newTimes;
                Log.Information($"检测到文件变更，已重新加载巡检任务配置，新配置包含 {_patrolExecutionTimes.Count} 个执行时间点\n      {string.Join("\n      ", _patrolExecutionTimes.Select(t => t.ToString(@"hh\:mm\:ss")))}");
                // 取消当前延迟，触发重新计算
                _delayCancellationTokenSource.Cancel();
            }
        }
        catch (Exception ex) {
            Log.Error(ex, "重新加载巡检任务配置失败");
        }
        finally {
            _reloadLock.Release();
        }
    }


    /// <summary>
    /// 初始加载巡检任务时间配置
    /// </summary>
    /// <returns></returns>
    private List<TimeSpan> LoadPatrolExecutionTimes() {
        try {
            var configPath = Path.Combine(_env.ContentRootPath, "Config", "TimerConfig.json");

            if (!File.Exists(configPath)) {
                Log.Warning("巡检任务配置文件不存在，使用默认执行时间");
                return new List<TimeSpan>
                {
                    TimeSpan.FromHours(8),
                    TimeSpan.FromHours(20)
                };
            }

            var json = File.ReadAllText(configPath);
            var configs = JsonConvert.DeserializeObject<List<TimerViewModel>>(json);

            return configs
                .Select(c => TimeSpan.ParseExact(c.Time, "hh\\:mm\\:ss", CultureInfo.InvariantCulture))
                .ToList();
        }
        catch (Exception ex) {
            Log.Error(ex, "解析定时任务配置文件失败，使用默认执行时间，请检查是否已配置定时任务");

            return new List<TimeSpan>
            {
                TimeSpan.FromHours(8),
                TimeSpan.FromHours(20)
            };
        }
    }


    /// <summary>
    /// 重载文件复制配置
    /// </summary>
    /// <returns></returns>
    private async Task ReloadCopyConfigAsync() {
        if (!_reloadLock.Wait(0))
            return;

        try {
            // 等待文件写入完成
            await Task.Delay(500);

            (_copyExecutionTime, _cycle) = LoadCopyExecutionTime();
            Log.Information($"检测到复制任务配置文件变更，已重新加载复制任务配置配置，新的配置执行时间\n      {_copyExecutionTime:hh\\:mm\\:ss}");
            // 取消当前延迟，触发重新计算
            _delayCancellationTokenSource.Cancel();
        }
        catch (Exception ex) {
            Log.Error(ex, "重新加载文件复制任务配置失败");
        }
        finally {
            _reloadLock.Release();
        }
    }


    /// <summary>
    /// 初始加载文件复制时间配置
    /// </summary>
    /// <returns></returns>
    private (DateTime, int) LoadCopyExecutionTime() {
        try {
            var configPath = Path.Combine(_env.ContentRootPath, "Config", "FileCopyConfig.json");
            if (!File.Exists(configPath)) {
                Log.Warning("文件复制任务配置文件不存在，使用默认执行时间");
                return (DateTime.Now.AddMilliseconds(100), 60);
            }

            var json = File.ReadAllText(configPath);
            var config = JsonConvert.DeserializeObject<CopyViewModel>(json);
            return (DateTime.Now.AddMilliseconds(100), config.Cycle);
        }
        catch (Exception ex) {
            Log.Error(ex, "解析复制任务配置文件失败，使用默认执行时间，请检查是否已配置执行时间");
            return (DateTime.Now.AddMilliseconds(100), 60);
        }
    }


    /// <summary>
    /// 定时任务执行函数
    /// </summary>
    /// <param name="stoppingToken">延迟令牌</param>
    /// <returns></returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        while (!stoppingToken.IsCancellationRequested) {
            try {
                // 配置文件重新加载时，重置延迟取消令牌（包括 TimerConfig 和 FileCopyConfig 两个文件）
                if (_delayCancellationTokenSource.IsCancellationRequested) {
                    _delayCancellationTokenSource.Dispose();
                    _delayCancellationTokenSource = new CancellationTokenSource();
                }

                // 合并两个取消令牌，用于当巡检配置变更时，取消当前延迟
                using var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                    _delayCancellationTokenSource.Token,
                    stoppingToken
                );

                var now = DateTime.Now;

                // 下一次任务执行时间
                var nextPatrolExecution = GetNextPatrolExecutionTime(now.TimeOfDay);
                var nextCopyExecution = GetNextCopyExecutionTime(now, _cycle);

                // 休眠时间差
                var delayPatrol = nextPatrolExecution - now.TimeOfDay;
                var delayCopy = nextCopyExecution - now;

                // 时间差为负数，表示今天的时间已经过去，需要计算明天的时间
                if (delayPatrol < TimeSpan.Zero) {
                    delayPatrol += TimeSpan.FromDays(1);
                    Log.Information($"当天无可执行巡检任务，进入休眠过程，下一次巡检任务执行时间: {DateTime.Today.AddDays(1).ToShortDateString()} {nextPatrolExecution}");
                }
                else {
                    Log.Information($"下一次巡检任务执行时间: {nextPatrolExecution}");
                }

                Log.Information($"下一次复制任务执行时间: {nextCopyExecution}");

                // 巡检任务和复制任务的延迟执行
                var patrolTask = Task.Run(async () => {
                    await Task.Delay(delayPatrol, linkedCancellationTokenSource.Token);

                    // 执行巡检任务
                    using (var scope = _serviceProvider.CreateScope()) {
                        var service = scope.ServiceProvider.GetRequiredService<ITimerService>();
                        await service.ExecuteScheduledTaskAsync();
                    }
                }, stoppingToken);

                var copyTask = Task.Run(async () => {
                    await Task.Delay(delayCopy, linkedCancellationTokenSource.Token);

                    // 执行复制任务
                    using (var scope = _serviceProvider.CreateScope()) {
                        var service = scope.ServiceProvider.GetRequiredService<ITimerService>();
                        await service.ExecuteFileCopyTaskAsync();
                    }
                }, stoppingToken);

                await Task.WhenAny(patrolTask, copyTask);
            }
            catch (OperationCanceledException) {
                // 检查是否因配置变更而取消
                if (_delayCancellationTokenSource.IsCancellationRequested && !stoppingToken.IsCancellationRequested) {
                    Log.Information("休眠过程已中断，定时任务继续执行");
                    continue;
                }
                Log.Information("定时任务服务被取消");
                break;
            }
            catch (Exception ex) {
                Log.Error(ex, "执行定时任务时发生错误");
                // 发生错误时等待1分钟再重试
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        // 清理资源
        _watcher.Dispose();
        _delayCancellationTokenSource.Dispose();
    }


    /// <summary>
    /// 获取下一个执行时间
    /// </summary>
    /// <param name="currentTime"></param>
    /// <returns></returns>
    private TimeSpan GetNextPatrolExecutionTime(TimeSpan currentTime) {
        var nextTime = _patrolExecutionTimes
            .Where(t => t > currentTime)
            .OrderBy(t => t)
            .FirstOrDefault();

        return nextTime == TimeSpan.Zero ? _patrolExecutionTimes.Min() : nextTime;
    }


    /// <summary>
    /// 获取复制任务下一次执行时间
    /// </summary>
    /// <param name="cycle">执行周期</param>
    /// <returns></returns>
    private DateTime GetNextCopyExecutionTime(DateTime now, int cycle) {
        _copyExecutionTime = _copyExecutionTime > now   
            ? _copyExecutionTime 
            : _copyExecutionTime.AddMinutes(cycle);

        return _copyExecutionTime ;
    }


    /// <summary>
    /// 将对应的文件名和回调函数注册到字典中
    /// </summary>
    /// <param name="fileName"></param>
    /// <param name="callback"></param>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="ArgumentNullException"></exception>
    private void RegisterFileCallback(string fileName, Action<FileSystemEventArgs> callback) {
        // 确保文件名不为空
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("文件名不能为空", nameof(fileName));

        // 确保回调函数不为空
        if (callback == null)
            throw new ArgumentNullException(nameof(callback));

        // 将文件名和回调函数添加到字典中
        _fileCallbacks[fileName] = callback;

        // 可选：记录注册信息
        Log.Information($"已注册文件监听: {fileName}");
    }


    /// <summary>
    /// 初始化文件监听回调
    /// </summary>
    private void RegisterFileCallbackInit() {
        RegisterFileCallback("TimerConfig.json", async e => {
            await ReloadPatrolConfigAsync();
        });

        RegisterFileCallback("FileCopyConfig.json", async e => {
            await ReloadCopyConfigAsync();
        });
    }
}