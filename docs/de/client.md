# Client-Bibliothek

Das NuGet-Paket [`MessageQueueNET.Client`](https://www.nuget.org/packages/MessageQueueNET.Client/)
(.NET Standard 2.1) kapselt die REST-API. Es enthält außerdem die Services für die Patterns
[Queue Worker](patterns/queue-worker.md) und [App Topic](patterns/app-topic.md).

```bash
dotnet add package MessageQueueNET.Client
```

!!! warning "Client- und Server-Version"
    Einen Client mit derselben Major-Version wie der Server verwenden. Clients älter als 4.0 können die
    4.x-API nicht aufrufen, weil sich einige HTTP-Methoden geändert haben.

## QueueClient

Ein `QueueClient` arbeitet auf einem Queue-Namen oder einem [Muster](api/rest.md#patterns).

=== "Zugangsdaten in der URL"

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

Der Konstruktor nimmt optional einen `HttpClient`. Ohne ihn wird ein gemeinsamer statischer `HttpClient` verwendet.

### Beispiel

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

1. Abgeholte Nachrichten müssen innerhalb von 60 Sekunden bestätigt werden, sonst kommen sie zurück in die Queue.
2. Erst bestätigen, wenn die Nachricht erfolgreich verarbeitet wurde.

### Methoden

Alle Methoden liefern ein von `ApiResult` abgeleitetes Ergebnis (`Success`, `ErrorMessage`, `HashCode`) und
akzeptieren ein optionales `CancellationToken`. Sie verhalten sich wie die [REST-Methoden](api/rest.md).

| Methode | REST |
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

HTTP-Antworten ohne Erfolgsstatus lösen eine `MessageQueueClientException` mit dem `StatusCode` aus.

## Dependency Injection und Long Polling

In ASP.NET Core und Worker Services den `MessageQueueClientService` registrieren. Er verwendet
`IHttpClientFactory`, prüft, ob die Major-Versionen von Client und Server übereinstimmen, und stellt
**Long-Polling**-Streams bereit:

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
                // message.Value verarbeiten
            }
        }
    }
}
```

1. Jede Iteration wartet am Server, bis neue Nachrichten eintreffen (Long Polling), statt in einer Schleife abzufragen.

`GetNextQueueProperties(...)` funktioniert genauso für Queue-Eigenschaften (wird vom Dashboard verwendet).

Für vollständige Consumer mit Workern, Ergebnissen und Bestätigungen das
[Queue-Worker-Pattern](patterns/queue-worker.md) verwenden.
