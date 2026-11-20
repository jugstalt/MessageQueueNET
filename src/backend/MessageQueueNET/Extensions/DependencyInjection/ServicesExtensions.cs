using MessageQueueNET.Services;
using MessageQueueNET.Services.Abstraction;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace MessageQueueNET.Extensions.DependencyInjection
{
    static public class ServicesExtensions
    {
        static public IServiceCollection AddQueuePersitFileSystem(this IServiceCollection services, Action<QueuePersistFileSystemOptions> setupAction)
        {
            Console.WriteLine("Using QueuePersistFileSystem service");

            services.Configure<QueuePersistFileSystemOptions>(setupAction);
            services.AddTransient<IQueuesPersistService, QueuePersistFileSystem>();

            return services;
        }

        static public IServiceCollection AddQueuePersitNone(this IServiceCollection services)
        {
            Console.WriteLine("Add QueuePersistNone service");

            return services.AddTransient<IQueuesPersistService, QueuePersistNone>();
        }

        static public IServiceCollection AddQueuesService(this IServiceCollection services)
        {
            services.AddOptions<MessageQueueLimitsOptions>()
                    .BindConfiguration("MessageQueue:Limits");

            return services.AddSingleton<QueuesService>();
        }

        static public IServiceCollection AddRestorePersistedQueuesService(this IServiceCollection services)
        {
            return services.AddTransient<RestorePersistedQueuesService>();
        }
    }
}
