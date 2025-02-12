using Autofac;
using Hangfire;
using IISState.Services;

namespace IISState.Core;

public class HangfireConfig
{
    public static void ConfigureHangfire(IServiceCollection services,IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        services.AddHangfire(config =>
        {
            config.UseSqlServerStorage(connectionString);
        });
        services.AddHangfireServer();

    }
    
    public static void ConfigureJobs()
    {
        // 在此处配置定时任务 "*/2 * * * *"
        RecurringJob.AddOrUpdate<MonitorService>("MonitorServiceJob",
            x => x.DoWork(),"*/2 * * * *"
            );
        // 每天凌晨2点执行一次,删除48小时前的数据
        RecurringJob.AddOrUpdate<MonitorService>("MonitorServiceJob2",
            x =>x.Del48HoursData(),"0 0 */2 * *");
    }
}