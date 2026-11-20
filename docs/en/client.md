# Client library

The NuGet package [`MessageQueueNET.Client`](https://www.nuget.org/packages/MessageQueueNET.Client/)
(.NET Standard 2.1) wraps the REST API. It also contains the services for the
[queue worker](patterns/queue-worker.md) and [app topic](patterns/app-topic.md) patterns.

```bash
dotnet add package MessageQueueNET.Client
```

!!! warning "Client and server version"
    Use a client with the same major version as the server. Clients older than 4.0 cannot call the
    4.x API because some HTTP methods changed.

## QueueClient

A `QueueClient` works on one queue name or [pattern](api/rest.md#patterns).

=== "Credentials in the URL"

    ```csharp
    using MessageQueueNET.Client;
    using MessageQueueNET.Client.Models;

    var client = new QueueClient(
        new MessageQueueConnection("https://worker:secret@mq.example.com"),
        "orders");
    ```

=== "BasicAuthentication"

    ```csharp
    using MessageQueueNET.Client;
    using MessageQueueNET.Client.Models;
    using MessageQueueNET.Client.Models.Authentication;

    var client = new QueueClient(
        new MessageQueueConnection("https://mq.example.com")
        {
            Authentication = new BasicAuthentication("worker", "secret")
        },
        "orders");
    ```

The constructor optionally takes an `HttpClient`. Without it, a shared static `HttpClient` is used.

### Example

```csharp
var client = new QueueClient(new MessageQueueConnection("http://localhost:9091"), "orders");

await client.RegisterAsync(confirmationPeriodSeconds: 60); // (1)!

await client.EnqueueAsync(["order #1", "order #2"]);

var length = await client.LengthAsync();               // length.Queues["orders"].QueueLength == 2

var result = await client.DequeueAsync(count: 1);
foreach (var message in result.Messages ?? [])
{
    Console.WriteLine(message.Value);                  // "order #1"
    await client.ConfirmDequeueAsync(message.Id);      // (2)!
}
```

1. Dequeued messages must be confirmed within 60 seconds, otherwise they go back into the queue.
2. Confirm only after the message was processed successfully.

### Methods

All methods return a result derived from `ApiResult` (`Success`, `ErrorMessage`, `HashCode`) and accept an
optional `CancellationToken`. They behave like the [REST methods](api/rest.md).

| Method | REST |
|---|---|
| `ApiInfoAsync()` | [`GET /Info`](api/rest.md#info) |
| `EnqueueAsync(messages)` | [`PUT /Queue/enqueue`](api/rest.md#enqueue) |
| `DequeueAsync(count, register, cancelationToken, hashCode, maxPollingSeconds)` | [`POST /Queue/dequeue`](api/rest.md#dequeue) |
| `ConfirmDequeueAsync(messageId)` | [`POST /Queue/confirmdequeue`](api/rest.md#confirm-dequeue) |
| `AllMessagesAsync(max, unconfirmedOnly)` | [`GET /Queue/all`](api/rest.md#all-messages) |
| `LengthAsync()` | [`GET /Queue/length`](api/rest.md#length) |
| `RegisterAsync(lifetimeSeconds, itemLifetimeSeconds, confirmationPeriodSeconds, maxUnconfirmedItems, maxUnconfirmedItemsStrategy, suspendEnqueue, suspendDequeue)` | [`PUT /Queue/register`](api/rest.md#register) |
| `PropertiesAsync(cancelationToken, hashCode, maxPollingSeconds, silentAccess)` | [`GET /Queue/properties`](api/rest.md#properties) |
| `QueueNamesAsync()` | [`GET /Queue/queuenames`](api/rest.md#queue-names-list) |
| `RemoveAsync(removeType)` | [`DELETE /Queue/remove`](api/rest.md#remove) |
| `DeleteMessage(messageId)` | [`DELETE /Queue/deletemessage`](api/rest.md#delete-message) |

Non-success HTTP responses throw a `MessageQueueClientException` with the `StatusCode`.

## Dependency injection and long polling

In ASP.NET Core and worker services, register the `MessageQueueClientService`. It uses `IHttpClientFactory`,
checks that client and server major versions match and provides **long polling** streams:

```csharp title="Program.cs"
using MessageQueueNET.Client.Extensions.DependencyInjetion;

builder.Services.AddMessageQueueClientService();
```

```csharp title="OrderConsumer.cs"
using MessageQueueNET.Client.Models;
using MessageQueueNET.Client.Services;

public class OrderConsumer(MessageQueueClientService mq) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var connection = new MessageQueueConnection("https://worker:secret@mq.example.com");

        await foreach (var result in mq.GetNextMessages(connection, "orders", stoppingToken)) // (1)!
        {
            foreach (var message in result.Messages ?? [])
            {
                // process message.Value
            }
        }
    }
}
```

1. Each iteration waits on the server until new messages arrive (long polling), instead of polling in a loop.

`GetNextQueueProperties(...)` works the same way for queue properties (used by the dashboard).

For complete consumers with workers, results and confirmations, use the
[queue worker pattern](patterns/queue-worker.md).
