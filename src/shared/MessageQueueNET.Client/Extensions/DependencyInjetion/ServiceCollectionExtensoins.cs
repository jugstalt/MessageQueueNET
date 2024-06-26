﻿using MessageQueueNET.Client.Services;
using MessageQueueNET.Client.Services.Abstraction;
using MessageQueueNET.Core.Extensions;
using MessageQueueNET.Core.Services.Abstraction;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.Net.Http;
using System.Security.Authentication;

namespace MessageQueueNET.Client.Extensions.DependencyInjetion;

static public class ServiceCollectionExtensoins
{
    static public IServiceCollection AddMessageQueueAppVersionService(this IServiceCollection services)
    {
        services.TryAddSingleton<IMessageQueueApiVersionService, MessageQueueApiVersionService>();

        return services;
    }

    static public IServiceCollection AddMessageQueueClientService(this IServiceCollection services)
    {
        services
            .AddHttpClient(MessageQueueClientService.HttpClientName, client =>
            {
                client.Timeout = TimeSpan.FromSeconds(90);
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
            {
                ServerCertificateCustomValidationCallback = (m, c, ch, e) => true,
                SslProtocols = SslProtocols.Tls11 | SslProtocols.Tls12 | /*SslProtocols.Tls13 |*/ SslProtocols.Tls
            });

        return services
            .AddMessageQueueAppVersionService()
            .AddTransient<MessageQueueClientService>();
    }

    static public IServiceCollection AddQueueProcessorWatcher(
                this IServiceCollection services,
                Action<QueueWatcherBackgroundServiceOptions> configAction
            )
        => services
            .Configure(configAction)
            .AddTransient<IQueueProcessor, PingWorker>()
            .AddHostedService<QueueWatcherBackgroundService>();

    static public IServiceCollection AddMessageQueueAppTopicServices(
                this IServiceCollection services,
                Action<MessageQueueAppTopicServiceOptions> configAction)
        => services
            .Configure(configAction)
            .AddHostedService<MessageQueueAppTopicHandlerBackgroundService>()
            .AddTransient<MessageQueueAppTopicService>();

    static public IServiceCollection AddMessageHandler<THandler>(
                this IServiceCollection services
            ) where THandler : class, IMessageHandler
    {
        {
            services.TryAddKeyedScoped<IMessageHandler, THandler>(typeof(THandler).MessageHandlerCommandName());
            return services;
        }
    }
}
