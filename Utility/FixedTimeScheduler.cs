using AutoPatrol.Models;
using AutoPatrol.Services;
using Newtonsoft.Json;
using System.Globalization;

public class FixedTimeScheduler : BackgroundService
{
    private List<TimeSpan> _executionTimes;
    private readonly IServiceProvider _serviceProvider;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<FixedTimeScheduler> _logger;
    private readonly FileSystemWatcher _watcher;
    private readonly SemaphoreSlim _reloadLock = new SemaphoreSlim(1, 1);

    public FixedTimeScheduler( IServiceProvider serviceProvider, IWebHostEnvironment env, ILogger<FixedTimeScheduler> logger) {
        _serviceProvider = serviceProvider;
        _env = env;
        _logger = logger;

        // 初始化执行时间
        _executionTimes = LoadExecutionTimes();

        // 初始化文件监听
        var configPath = Path.Combine(_env.ContentRootPath, "Config", "TimerConfig.json");
        var directory = Path.GetDirectoryName(configPath);
        var fileName = Path.GetFileName(configPath);

        _watcher = new FileSystemWatcher(directory, fileName) {
            EnableRaisingEvents = true,
            NotifyFilter = NotifyFilters.LastWrite
        };

        _watcher.Changed += OnConfigFileChanged;
    }

    private void OnConfigFileChanged(object sender, FileSystemEventArgs e) {
        // 使用异步方法的最佳实践：不要在事件处理程序中直接使用 async void
        // 而是启动一个独立的异步任务
        _ = ReloadConfigAsync();
    }

    private async Task ReloadConfigAsync() {
        // 防止多个变更同时触发
        if (!_reloadLock.Wait(0))
            return;

        try {
            // 等待文件写入完成
            await Task.Delay(500);

            var newTimes = LoadExecutionTimes();
            if (newTimes != null && newTimes.Any()) {
                _executionTimes = newTimes;
                _logger.LogInformation("已重新加载定时任务配置，新配置包含 {Count} 个执行时间点", _executionTimes.Count);
            }
        }
        catch (Exception ex) {
            _logger.LogError(ex, "重新加载定时任务配置失败");
        }
        finally {
            _reloadLock.Release();
        }
    }

    private List<TimeSpan> LoadExecutionTimes() {
        try {
            var configPath = Path.Combine(_env.ContentRootPath, "Config", "TimerConfig.json");

            if (!File.Exists(configPath)) {
                _logger.LogWarning("定时任务配置文件不存在，使用默认执行时间");
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
            _logger.LogError(ex, "解析定时任务配置文件失败，使用默认执行时间");
            return new List<TimeSpan>
            {
                TimeSpan.FromHours(8),
                TimeSpan.FromHours(20)
            };
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        while (!stoppingToken.IsCancellationRequested) {
            try {
                var now = DateTime.Now.TimeOfDay;
                var nextExecution = GetNextExecutionTime(now);
                var delay = nextExecution - now;

                if (delay < TimeSpan.Zero) {
                    delay += TimeSpan.FromDays(1);
                }

                _logger.LogInformation($"下一次任务执行时间: {DateTime.Today.AddDays(1).ToShortDateString()} {nextExecution}");
                await Task.Delay(delay, stoppingToken);

                using (var scope = _serviceProvider.CreateScope()) {
                    var service = scope.ServiceProvider.GetRequiredService<ITimerService>();
                    await service.ExecuteScheduledTaskAsync();
                }
            }
            catch (OperationCanceledException) {
                _logger.LogInformation("定时任务服务被取消");
                break;
            }
            catch (Exception ex) {
                _logger.LogError(ex, "执行定时任务时发生错误");
                // 发生错误时等待1分钟再重试
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        // 清理资源
        _watcher.Dispose();
    }

    private TimeSpan GetNextExecutionTime(TimeSpan currentTime) {
        var nextTime = _executionTimes
            .Where(t => t > currentTime)
            .OrderBy(t => t)
            .FirstOrDefault();

        return nextTime == TimeSpan.Zero ? _executionTimes.Min() : nextTime;
    }
}