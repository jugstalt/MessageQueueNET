using MessageQueueNET.Client.Extensions.DependencyInjetion;
using MessageQueueNET.Razor.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MessageQueueNET.Razor.Extensions.DependencyInjection;

static public class ServiceCollectionExtensions
{
    static public IServiceCollection AddDashboardService(this IServiceCollection services,
                                                         Action<DashboardServiceOptions> setupAction)
    {
        return services
            .Configure(setupAction)
            .AddSingleton<QueryQueueEventBus>()
            .AddHostedService<QueryQueueBackgroundService>()
            .AddTransient<DashboardEventBusService>()
            .AddMessageQueueClientService()
            .AddScoped<DashboardService>()
            .AddScoped<QueueHistoryService>();
    }
}
