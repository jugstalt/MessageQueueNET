using MessageQueueNET.Client;
using MessageQueueNET.Client.Models;
using MessageQueueNET.Client.Services;
using MessageQueueNET.Razor.Extensions;
using Microsoft.Extensions.Options;

namespace MessageQueueNET.Razor.Services;

internal class DashboardService
{
    private readonly DashboardEventBusService _eventBus;
    private readonly DashboardServiceOptions _options;
    private readonly MessageQueueClientService _clientService;

    public DashboardService(DashboardEventBusService eventBus,
                            MessageQueueClientService clientService,
                            IOptions<DashboardServiceOptions> options)
    {
        _eventBus = eventBus;
        _clientService = clientService;
        _options = options.Value;

        SelectedServerName = _options.Queues?.FirstOrDefault()?.Name ?? String.Empty;
    }

    public DashboardEventBusService EventBus => _eventBus;

    public IEnumerable<string> QueueServersNames =>
        _options.Queues?.Select(q => q.Name) ?? Array.Empty<string>();

    public string SelectedServerName { get; private set; }
    public string? SelectedServerFilter => _options.Queues?.FirstOrDefault(q => q.Name == SelectedServerName)?.Filter;
    public string? SelectedServerUrl => _options.Queues?.FirstOrDefault(q => q.Name == SelectedServerName)?.Url;

    async public Task SetSelectedServerName(string serverName)
    {
        this.SelectedServerName = serverName;

        await _eventBus.FireQueueServerChangedAsync();
    }

    async public Task<MessagesResult> GetAllQueueMessages(string queueName, bool unconfirmed)
    {
        var client = await _options.GetQueueClient(_clientService, SelectedServerName, queueName);
        var messages = await client.AllMessagesAsync(unconfirmedOnly: unconfirmed);

        return messages;
    }

    async public Task<QueueProperties> GetQueueProperties(string queueName)
    {
        var client = await _options.GetQueueClient(_clientService, SelectedServerName, queueName);
        var propertiesResult = await client.PropertiesAsync();

        if (propertiesResult?.Queues == null ||
           propertiesResult.Queues.ContainsKey(queueName) == false)
        {
            throw new Exception($"Can't get properties for queue: {queueName}");
        }

        return propertiesResult.Queues[queueName];
    }

    async public Task<bool> SetQueueProperties(string queueName, QueueProperties queueProperties)
    {
        var client = await _options.GetQueueClient(_clientService, SelectedServerName, queueName);

        await client.RegisterAsync(
            lifetimeSeconds: queueProperties.LifetimeSeconds,
            itemLifetimeSeconds: queueProperties.ItemLifetimeSeconds,

            confirmationPeriodSeconds: queueProperties.ConfirmationPeriodSeconds,
            maxUnconfirmedItems: queueProperties.MaxUnconfirmedItems,
            maxUnconfirmedItemsStrategy: queueProperties.MaxUnconfirmedItemsStrategy,

            suspendEnqueue: queueProperties.SuspendEnqueue,
            suspendDequeue: queueProperties.SuspendDequeue);

        return true;
    }

    async public Task<bool> SetQueueSuspendEnqueue(string queueName, bool suspend)
    {
        var client = await _options.GetQueueClient(_clientService, SelectedServerName, queueName);

        await client.RegisterAsync(suspendEnqueue: suspend);

        return true;
    }

    async public Task<bool> SetQueueSuspendDequeue(string queueName, bool suspend)
    {
        var client = await _options.GetQueueClient(_clientService, SelectedServerName, queueName);

        await client.RegisterAsync(suspendDequeue: suspend);

        return true;
    }

    async public Task<bool> SetQueuesSuspend(string queueName, bool suspendEnqueue, bool suspendDequeue)
    {
        var client = await _options.GetQueueClient(_clientService, SelectedServerName, queueName);

        await client.RegisterAsync(
            suspendEnqueue: suspendEnqueue,
            suspendDequeue: suspendDequeue);

        return true;
    }

    async public Task<bool> AddMessages(string queueName, IEnumerable<string> messages)
    {
        var client = await _options.GetQueueClient(_clientService, SelectedServerName, queueName);

        return (await client.EnqueueAsync(messages)).Success;
    }

    async public Task<bool> DeleteQueue(string queueName, RemoveType removeType)
    {
        var client = await _options.GetQueueClient(_clientService, SelectedServerName, queueName);

        return (await client.RemoveAsync(removeType)).Success;
    }

    async public Task<bool> DeleteMessage(string queueName, Guid messageId)
    {
        var client = await _options.GetQueueClient(_clientService, SelectedServerName, queueName);

        return (await client.DeleteMessage(messageId)).Success;
    }

    public bool CanDeleteMany()
        => !string.IsNullOrEmpty(DeleteManyQueuePattern());

    public string DeleteManyQueuePattern()
        => _options.DeleteQueueNamePattern(SelectedServerName);

    public bool CanCreateQueue()
        => !String.IsNullOrEmpty(CreateQueuePattern());

    public string CreateQueuePattern()
        => _options.NewQueueNamePattern(SelectedServerName);

    async public Task<bool> CreateQueue(string queueName)
    {
        if (String.IsNullOrEmpty(queueName))
        {
            return false;
        }

        if (!queueName.StartsWith(CreateQueuePattern().Replace("*", "")))
        {
            queueName = CreateQueuePattern().Replace("*", queueName);
        }

        var client = await _options.GetQueueClient(_clientService, SelectedServerName, queueName);

        await client.RegisterAsync();

        return true;
    }
}
