using Autofac;
using IISState.Models;
using IISState.Services;
using Microsoft.EntityFrameworkCore;

namespace IISState.Core;

public class AutofacRegister:Autofac.Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<MonitorContext>().As<DbContext>().InstancePerLifetimeScope();
        builder.RegisterType<MonitorService>().AsSelf().InstancePerLifetimeScope();
        builder.RegisterType<WeChatRobotService>().AsSelf().InstancePerLifetimeScope();
        builder.Register(c =>
        {
            var httpClientFactory = c.Resolve<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient();
            var configguration = c.Resolve<IConfiguration>();
            return new WeChatRobotService(httpClient, configguration);
        }).As<WeChatRobotService>().InstancePerLifetimeScope();
    }
}