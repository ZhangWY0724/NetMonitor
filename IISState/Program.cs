using Autofac;
using Autofac.Extensions.DependencyInjection;
using Hangfire;
using IISState.Core;
using IISState.Filter;
using IISState.Hubs;
using IISState.Models;
using IISState.Services;
using Microsoft.EntityFrameworkCore;
using WatchDog;
using WatchDog.src.Enums;

namespace IISState;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddHttpClient();
        builder.Services.AddMemoryCache();
        // 添加数据库上下文
        builder.Services.AddDbContext<MonitorContext>(options =>
            options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
                .UseLoggerFactory(LoggerFactory.Create(builder =>
                {
                    builder.AddConsole().SetMinimumLevel(LogLevel.Information);
                    builder.AddWatchDogLogger();
                })));
        
            ;
        
        //autofac
        builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
        builder.Host.ConfigureContainer<ContainerBuilder>(builder => { builder.RegisterModule<AutofacRegister>(); });

        //hangfire
        HangfireConfig.ConfigureHangfire(builder.Services, builder.Configuration);

        // 添加控制器
        builder.Services.AddControllers();
        builder.Services.AddSignalR();
        // 添加 Swagger
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        
        //watchdog添加
        builder.Services.AddWatchDogServices(options =>
        {
            options.IsAutoClear = true; //是否自动清理过期数据
            options.ClearTimeSchedule = WatchDogAutoClearScheduleEnum.Every6Hours; //清理时间间隔
            options.DbDriverOption = WatchDogDbDriverEnum.MSSQL; //数据库驱动
            options.SetExternalDbConnString = builder.Configuration.GetConnectionString("DefaultConnection"); //数据库连接字符串
        });

        builder.Logging.AddWatchDogLogger();

        var app = builder.Build();

        // 配置 HTTP 请求管道
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseStaticFiles();
        // 设置默认文件为 index.html
        var options = new DefaultFilesOptions();
        options.DefaultFileNames.Clear();
        options.DefaultFileNames.Add("index.html");
        app.UseDefaultFiles(options);

        // 启用 Hangfire 仪表盘
        app.UseHangfireDashboard("/hangfire", new DashboardOptions
        {
            Authorization = [new DashboardNoAuthorizationFilter()]
        });
        HangfireConfig.ConfigureJobs();
        // 启用路由
        app.UseRouting();
        app.UseWatchDogExceptionLogger();
        app.UseWatchDog(options =>
        {
            options.WatchPageUsername = "admin";
            options.WatchPagePassword = "admin";
        });
        // 配置端点
        app.MapControllers();
        
        app.MapHub<NotificationHub>("/monitorHub");
        

        // 初始化数据库
        using (var scope = app.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MonitorContext>();
            dbContext.Database.EnsureCreated();
        }

        app.Run();
    }
}