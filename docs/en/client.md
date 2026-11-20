# MessageQueueNET.Client

The `MessageQueueNET.Client` .NET Standard Library (nuget) contains the `QueueClient` class, which provides an abstraction of the REST interface using any .NET language.

https://www.nuget.org/packages/MessageQueueNET.Client

## Instantiation

A client can be created as follows:

```csharp
var client = new QueueClient("https://clientId:clientSecret@my-server", "my-queue-1");
```

If API authorization is used, the `ClientId` and `ClientSecret` can be passed in url-format (example above) or as optional parameters.

## Methods

Here all methods are listed. The function is the same as described for the REST API:

```csharp
Task<InfoResult> ApiInfoAsync()
```

```csharp
Task<ApiResult> EnqueueAsync(IEnumerable<string> messages)
```

```csharp
Task<MessagesResult> DequeueAsync(
            int count = 1,
            bool register = false,
            CancellationToken? cancelationToken = null,
            int? hashCode = null)
```

```csharp
Task<ApiResult> ConfirmDequeueAsync(Guid messageId)
```

```csharp
Task<MessagesResult> AllMessagesAsync(int max = 0, bool unconfirmedOnly = false)
```

```csharp
Task<QueueLengthResult> LengthAsync()
```

```csharp
Task<ApiResult> RemoveAsync(RemoveType removeType = RemoveType.Queue)
```

```csharp
Task<ApiResult> DeleteMessage(Guid messageId)
```

```csharp
Task<QueuePropertiesResult> RegisterAsync(int? lifetimeSeconds = null,
                                          int? itemLifetimeSeconds = null,
                                          int? confirmationPeriodSeconds = null,
                                          int? maxUnconfirmedItems = null,
                                          MaxUnconfirmedItemsStrategy? maxUnconfirmedItemsStrategy = null,
                                          bool? suspendEnqueue = null,
                                          bool? suspendDequeue = null)
```

```csharp
Task<QueuePropertiesResult> PropertiesAsync(CancellationToken? cancelationToken = null,
                                            int? hashCode = null)
```

```csharp
Task<QueueNamesResult> QueueNamesAsync()
```

Example:

```csharp
var client = new QueueClient("https://clientId:clientSecret@my-server", "my-queue-1");

await client.Enqueue(new [] { "Message1", "Messsage2" });

var queueNames = await client.QueueNames(); // => ["my-queue-1, ..."]

var length = await client.Length(); // => 2
var message = await client.Dequeue(); // => ["Message1"]
```

[Commandline Tools](../console/tools_en.md)
