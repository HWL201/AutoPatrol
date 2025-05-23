using AutoPatrol.Services;
using AutoPatrol.Utility;

namespace AutoPatrol
{
    public class Program
    {
        public static void Main(string[] args) {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddControllersWithViews();

            builder.Services.AddScoped<ITimerService, TimerService>();

            // 添加日志服务
            builder.Services.AddLogging(loggingBuilder => {
                loggingBuilder.AddConsole();
                loggingBuilder.AddDebug();
            });

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

            app.Run();
        }
    }
}