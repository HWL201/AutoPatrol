using AutoPatrol.Services;
using AutoPatrol.Utility;
using Serilog;
using Serilog.Events;

namespace AutoPatrol
{
    public class Program
    {
        public static void Main(string[] args) {
            var builder = WebApplication.CreateBuilder(args);

            // 配置Serilog
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("System", LogEventLevel.Warning)
                .WriteTo.File(
                    Path.Combine("Log", ".log"),
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            Console.WriteLine(Path.Combine("Log", $"{DateTime.Now:yyyyMMdd}.log"));

            builder.Host.UseSerilog();

            builder.Services.AddControllersWithViews();

            builder.Services.AddScoped<ITimerService, TimerService>();

            // 注册定时任务服务
            builder.Services.AddHostedService<FixedTimeScheduler>();

            // 绑定配置文件中的 "DeviceTagCfg" 节点到 DeviceTagConfig 类
            builder.Services.Configure<MqConfig>(builder.Configuration.GetSection("DeviceTagCfg"));

            // 注册 MQServer 
            builder.Services.AddSingleton<MQServer>();

            var app = builder.Build();

            if (!app.Environment.IsDevelopment()) {
                app.UseExceptionHandler("/Home/Error");
            }
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthorization();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            try
            {
                Log.Information("应用程序启动");
                app.Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "应用程序启动失败");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}