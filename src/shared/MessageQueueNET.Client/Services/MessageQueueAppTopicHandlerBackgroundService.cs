using MessageQueueNET.Client.Extensions;
using MessageQueueNET.Core.Services.Abstraction;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MessageQueueNET.Client.Services;
internal class MessageQueueAppTopicHandlerBackgroundService : BackgroundService
{
    private readonly ILogger<MessageQueueAppTopicHandlerBackgroundService> _logger;
    private readonly MessageQueueClientService _clientService;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly MessageQueueAppTopicServiceOptions _options;

    public MessageQueueAppTopicHandlerBackgroundService(
                ILogger<MessageQueueAppTopicHandlerBackgroundService> logger,
                MessageQueueClientService clientService,
                IServiceScopeFactory serviceScopeFactory,
                IOptions<MessageQueueAppTopicServiceOptions> options
            )
    {
        _logger = logger;
        _clientService = clientService;
        _serviceScopeFactory = serviceScopeFactory;
        _options = options.Value;
    }

    async protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            if (String.IsNullOrEmpty(_options.MessageQueueApiUrl))
            {
                throw new Exception("No MessageQueueApiUrl defined");
            }

            var connection = _options.ToConnection();

            if (_options.ManageQueueLifetimeCycle)
            {
                var client = await _clientService.CreateClient(connection, _options.ToQueueName());
                await client.RegisterAsync(
                            lifetimeSeconds: _options.QueueLifetimeSeconds,
                            itemLifetimeSeconds: _options.ItemLifetimeSeconds
                        );
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var queueFilter = _options.ToTopicQueuesFilter();

                    await foreach (var messagesResult in _clientService.GetNextMessages(
                                connection,
                                queueFilter,
                                stoppingToken,
                                maxPollingSeconds: _options.MaxPollingSeconds
                            )
                        )
                    {
                        if (messagesResult.Messages?.Any() != true)
                        {
                            continue;
                        }

                        foreach (var messageResult in messagesResult.Messages)
                        {
                            try
                            {
                                if (messageResult?.Value?.Contains(":") != true)
                                {
                                    continue;
                                }

                                (string commandName, string commandMessage)
                                    = messageResult.Value.SplitByFirst(':');

                                using (var scope = _serviceScopeFactory.CreateScope())
                                {
                                    var messageHandler = scope.ServiceProvider.GetKeyedService<IMessageHandler>(commandName);

                                    if (messageHandler is null)
                                    {
                                        _logger.LogWarning("MessageHandler (Command={messageHandlerCommand}) is not a registered service", commandName);
                                    }
                                    else
                                    {
                                        await messageHandler.InvokeAsync(commandMessage);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning("Error in handling message {messageResult}: {exceptionMessage}", messageResult?.Value ?? "", ex.Message);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError("Exception in BackgroundService Loop: {message}", ex.Message);
                }
            }

            if (_options.ManageQueueLifetimeCycle)
            {
                var client = await _clientService.CreateClient(connection, _options.ToQueueName());
                await client.RemoveAsync(RemoveType.Queue);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Error in MessageQueueAppTopicHandlerBackgroundService (Background service is not running): {message}", ex.Message);
        }
    }
}
