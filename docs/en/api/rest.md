# REST reference

All queue methods live under `/Queue`. Every response contains `success` and, on errors, `errorMessage`
(see [Errors](configuration.md#errors)). Responses of queue methods also contain a `hashCode` that can be used
for [long polling](configuration.md#long-polling).

| Method | Route | Role | Description |
|---|---|---|---|
| `GET` | [`/Info`](#info) | `read` | Version of the API |
| `PUT` | [`/Queue/enqueue/{id}`](#enqueue) | `write` | Add messages to a queue |
| `POST` | [`/Queue/dequeue/{idPattern}`](#dequeue) | `write` | Take messages from one or more queues |
| `POST` | [`/Queue/confirmdequeue/{id}`](#confirm-dequeue) | `write` | Confirm a processed message |
| `GET` | [`/Queue/all/{idPattern}`](#all-messages) | `read` | Read messages without removing them |
| `GET` | [`/Queue/length/{idPattern}`](#length) | `read` | Number of messages |
| `PUT` | [`/Queue/register/{idPattern}`](#register) | `write`/`admin` | Create a queue or change its properties |
| `GET` | [`/Queue/properties/{idPattern}`](#properties) | `read` | Properties and state of queues |
| `GET` | [`/Queue/queuenames`](#queue-names-list) | `read` | Names of all queues |
| `DELETE` | [`/Queue/remove/{idPattern}`](#remove) | `write`/`admin` | Remove queues or their messages |
| `DELETE` | [`/Queue/deletemessage/{id}`](#delete-message) | `admin` | Delete a single message |

Methods with an `idPattern` need the `admin` role when the pattern matches more than one queue
(`register`, `remove`). See [Roles](configuration.md#roles).

## Queue names

A queue is identified by a unique name (`id`). Names may contain lowercase letters, digits, `_`, `-` and `.`:

- a name starts with a lowercase letter or digit,
- a period must be followed by a letter, digit, `_` or `-` (no `..`, no trailing period),
- a name has at most 128 characters.

Valid: `queue1`, `test.queue`, `appname.main.in`, `my_queue-123`.
Invalid: `.`, `..`, `a..b`, `queue.`, `_queue`.

### Patterns

Methods with an `idPattern` work on several queues at once. A pattern is a comma-separated list of names and
wildcard expressions:

| Pattern | Matches |
|---|---|
| `queue1,queue2` | `queue1` and `queue2` |
| `test.*` | all queues starting with `test.` |
| `queue1,test.*` | `queue1` and all queues starting with `test.` |

## Info

`GET /Info` returns the version of the API.

=== "HTTP"

    ```http
    GET /Info
    ```

=== "curl"

    ```bash
    curl -u admin-client:secret http://localhost:9091/Info
    ```

=== "C#"

    ```csharp
    var info = await client.ApiInfoAsync();
    Console.WriteLine(info.Version);
    ```

```json title="Response"
{
  "success": true,
  "version": {
    "major": 4,
    "minor": 0,
    "build": 0,
    "revision": 0,
    "majorRevision": 0,
    "minorRevision": 0
  }
}
```

## Enqueue

`PUT /Queue/enqueue/{id}` adds messages to a queue. The body is a JSON array of strings. A missing queue is created.

Enqueue is *all or nothing*: if one message cannot be stored, no message is enqueued.

=== "HTTP"

    ```http
    PUT /Queue/enqueue/test.queue1
    Content-Type: application/json

    ["Hello World", "Test Message"]
    ```

=== "curl"

    ```bash
    curl -u worker:secret -X PUT http://localhost:9091/Queue/enqueue/test.queue1 \
         -H "Content-Type: application/json" \
         -d '["Hello World", "Test Message"]'
    ```

=== "C#"

    ```csharp
    await client.EnqueueAsync(["Hello World", "Test Message"]);
    ```

=== "mqnet"

    ```bash
    mqnet http://localhost:9091 -q test.queue1 -c enqueue -m "Hello World"
    ```

```json title="Response"
{ "success": true }
```

## Dequeue

`POST /Queue/dequeue/{idPattern}` takes messages from one or more queues. The messages are removed and not
available for other consumers anymore.

| Parameter | In | Default | Description |
|---|---|---|---|
| `idPattern` | path | | Queue name or [pattern](#patterns) |
| `count` | query | `1` | Number of messages (limited by `MaxDequeueCount`) |
| `register` | query | `false` | No effect, kept for compatibility |

If a queue does not exist, it is created and `[]` is returned. Other processes can then see with
[`queuenames`](#queue-names-list) that somebody consumes this queue.

!!! info "Confirmation"
    If the queue has a `confirmationPeriodSeconds > 0`, the message has `requireConfirmation: true`. Confirm it
    with [`confirmdequeue`](#confirm-dequeue) within this period, otherwise it goes back into the queue.

=== "HTTP"

    ```http
    POST /Queue/dequeue/test.*?count=10
    ```

=== "curl"

    ```bash
    curl -u worker:secret -X POST "http://localhost:9091/Queue/dequeue/test.*?count=10"
    ```

=== "C#"

    ```csharp
    var result = await client.DequeueAsync(count: 10);

    foreach (var message in result.Messages ?? [])
    {
        Console.WriteLine($"{message.Queue}: {message.Value}");
    }
    ```

=== "mqnet"

    ```bash
    mqnet http://localhost:9091 -q test.queue1 -c dequeue
    ```

```json title="Response"
{
  "success": true,
  "hashCode": 1234567,
  "messages": [
    {
      "queue": "test.queue1",
      "id": "d290f1ee-6c54-4b01-90e6-d701748f0851",
      "value": "Hello World",
      "creationDateUTC": "2025-11-03T15:01:02.123Z",
      "requireConfirmation": true,
      "confirmationPeriod": 60
    }
  ]
}
```

## Confirm dequeue

`POST /Queue/confirmdequeue/{id}?messageId={messageId}` confirms that a message was processed. Only then is the
message finally removed from the queue.

With authentication enabled, only the client that dequeued the message can confirm it.

=== "HTTP"

    ```http
    POST /Queue/confirmdequeue/test.queue1?messageId=d290f1ee-6c54-4b01-90e6-d701748f0851
    ```

=== "curl"

    ```bash
    curl -u worker:secret -X POST \
         "http://localhost:9091/Queue/confirmdequeue/test.queue1?messageId=d290f1ee-6c54-4b01-90e6-d701748f0851"
    ```

=== "C#"

    ```csharp
    await client.ConfirmDequeueAsync(message.Id);
    ```

```json title="Response"
{ "success": true }
```

## All messages

`GET /Queue/all/{idPattern}` returns the messages of queues without changing them.

| Parameter | In | Default | Description |
|---|---|---|---|
| `max` | query | `0` | Maximum number of messages (`0`: all) |
| `unconfirmedOnly` | query | `false` | Only messages that were dequeued but not confirmed yet (only queues with `confirmationPeriodSeconds > 0`) |

=== "HTTP"

    ```http
    GET /Queue/all/test.*?max=100
    ```

=== "curl"

    ```bash
    curl -u dashboard:secret "http://localhost:9091/Queue/all/test.*?max=100"
    ```

=== "C#"

    ```csharp
    var result = await client.AllMessagesAsync(max: 100);
    ```

The response has the same format as [dequeue](#dequeue).

## Length

`GET /Queue/length/{idPattern}` returns the number of messages per queue.

=== "HTTP"

    ```http
    GET /Queue/length/test.*
    ```

=== "curl"

    ```bash
    curl -u dashboard:secret http://localhost:9091/Queue/length/test.*
    ```

=== "C#"

    ```csharp
    var result = await client.LengthAsync();
    ```

```json title="Response"
{
  "success": true,
  "queues": {
    "test.queue1": { "queueLength": 1, "unconfirmedItems": 0 }
  }
}
```

## Register

`PUT /Queue/register/{idPattern}` creates a queue (if it does not exist) or changes the properties of existing
queues. All parameters are optional query parameters; omitted parameters keep their current value.

| Property | Type | Description |
|---|---|---|
| `lifetimeSeconds` | int | The queue is deleted after it was empty and unused for this time. `0`: never. |
| `itemLifetimeSeconds` | int | Messages not picked up within this time are deleted. `0`: never. |
| `confirmationPeriodSeconds` | int | Dequeued messages must be [confirmed](#confirm-dequeue) within this time, otherwise they go back into the queue. `0`: no confirmation. |
| `maxUnconfirmedItems` | int | Maximum number of dequeued but unconfirmed messages. When reached, `dequeue` returns nothing until messages are confirmed. |
| `maxUnconfirmedItemsStrategy` | int | `0` *Absolute*: the maximum applies to the queue. `1` *PerClient*: the maximum applies to each client. |
| `suspendEnqueue` | bool | `true`: no messages can be added. |
| `suspendDequeue` | bool | `true`: no messages can be taken. The messages are kept. |

=== "HTTP"

    ```http
    PUT /Queue/register/orders?lifetimeSeconds=3600&confirmationPeriodSeconds=60
    ```

=== "curl"

    ```bash
    curl -u worker:secret -X PUT \
         "http://localhost:9091/Queue/register/orders?lifetimeSeconds=3600&confirmationPeriodSeconds=60"
    ```

=== "C#"

    ```csharp
    await client.RegisterAsync(lifetimeSeconds: 3600, confirmationPeriodSeconds: 60);
    ```

=== "mqnet"

    ```text
    >> orders register
    ```

The response has the same format as [properties](#properties).

## Properties

`GET /Queue/properties/{idPattern}` returns the properties and the state of queues.
Requests with the header `X-MQ-SilentAccess: true` do not update `lastAccessUTC` (used by the dashboard).

=== "HTTP"

    ```http
    GET /Queue/properties/orders
    ```

=== "curl"

    ```bash
    curl -u dashboard:secret http://localhost:9091/Queue/properties/orders
    ```

=== "C#"

    ```csharp
    var result = await client.PropertiesAsync();
    ```

```json title="Response"
{
  "success": true,
  "hashCode": 1234567,
  "queues": {
    "orders": {
      "lastAccessUTC": "2025-11-03T15:01:02.123Z",
      "lastModifiedUTC": "2025-11-03T15:01:02.123Z",
      "length": 0,
      "unconfirmedItems": 0,
      "dequeuingClientsCount": 0,
      "dequeuingClients": {},
      "lifetimeSeconds": 3600,
      "itemLifetimeSeconds": 0,
      "confirmationPeriodSeconds": 60,
      "maxUnconfirmedItems": null,
      "maxUnconfirmedItemsStrategy": 0,
      "suspendEnqueue": false,
      "suspendDequeue": false
    }
  }
}
```

## Queue names (list)

`GET /Queue/queuenames` returns the names of all queues.

=== "HTTP"

    ```http
    GET /Queue/queuenames
    ```

=== "curl"

    ```bash
    curl -u dashboard:secret http://localhost:9091/Queue/queuenames
    ```

=== "C#"

    ```csharp
    var result = await client.QueueNamesAsync();
    ```

```json title="Response"
{ "success": true, "queueNames": [ "orders", "test.queue1" ] }
```

## Remove

`DELETE /Queue/remove/{idPattern}?removeType={removeType}` removes queues or their messages.

| `removeType` | Description |
|---|---|
| `0` (default) | Remove the queue |
| `1` | Remove all messages |
| `2` | Remove the unconfirmed messages |

=== "HTTP"

    ```http
    DELETE /Queue/remove/test.*?removeType=0
    ```

=== "curl"

    ```bash
    curl -u admin-client:secret -X DELETE "http://localhost:9091/Queue/remove/test.*?removeType=0"
    ```

=== "C#"

    ```csharp
    await client.RemoveAsync(RemoveType.Queue);
    ```

```json title="Response"
{ "success": true }
```

## Delete message

`DELETE /Queue/deletemessage/{id}?messageId={messageId}` deletes a single message from a queue.

=== "HTTP"

    ```http
    DELETE /Queue/deletemessage/test.queue1?messageId=d290f1ee-6c54-4b01-90e6-d701748f0851
    ```

=== "curl"

    ```bash
    curl -u admin-client:secret -X DELETE \
         "http://localhost:9091/Queue/deletemessage/test.queue1?messageId=d290f1ee-6c54-4b01-90e6-d701748f0851"
    ```

=== "C#"

    ```csharp
    await client.DeleteMessage(message.Id);
    ```

```json title="Response"
{ "success": true }
```
