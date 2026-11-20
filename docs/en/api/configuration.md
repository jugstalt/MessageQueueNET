# Configuration

The REST API reads its settings from `appsettings.json`, from `_config/message-queue.json` and from environment
variables (`MESSAGEQUEUE__PERSIST__TYPE`, …).

!!! tip "Use `_config/message-queue.json`"
    Put your settings into `_config/message-queue.json`. This file is never overwritten by an update: to update,
    copy everything except the `_config` folder over an existing installation.

## Example

```json title="_config/message-queue.json"
{
  "MessageQueue": {
    "Persist": {
      "Type": "filesystem",
      "RootPath": "C:\\temp\\message-queue\\persist"
    },
    "Limits": {
      "MaxQueues": 10000,
      "MaxMessageBytes": 1048576,
      "MaxDequeueCount": 1000,
      "MaxQueueLength": 0
    },
    "MaxRequestPollingSeconds": 60
  },
  "Authorization": {
    "Type": "Basic",
    "Clients": [
      { "Id": "admin-client", "Secret": "sha256:<sha256 hex of the secret>", "Roles": [ "admin" ] },
      { "Id": "worker",       "Secret": "pas3w0rd2",                         "Roles": [ "write" ] },
      { "Id": "dashboard",    "Secret": "pas3w0rd3",                         "Roles": [ "read" ] }
    ]
  },
  "openApiUI": false
}
```

## Persistence

By default, queues are kept **in memory**. This is fast, but all queues are gone when the application stops or
is recycled.

With `MessageQueue:Persist:Type` set to `filesystem`, every change is additionally written to `RootPath`.
On startup, the application restores the last state. Very long queues can slow down the startup a little.

| Setting | Description |
|---|---|
| `Persist:Type` | `filesystem` or empty (in memory only) |
| `Persist:RootPath` | Folder for the persisted queues |

!!! note
    Persisted queues whose names are no longer valid (see [queue names](rest.md#queue-names)) are skipped on startup.
    A warning is logged, the files are not deleted.

## Limits

`MessageQueue:Limits` protects the service against resource exhaustion. A value `<= 0` means *unlimited*.

| Setting | Default | Description |
|---|---|---|
| `MaxQueues` | `10000` | Maximum number of queues. Creating further queues fails. |
| `MaxMessageBytes` | `1048576` | Maximum size of a single message (UTF-8 bytes). |
| `MaxDequeueCount` | `1000` | Upper bound for the `count` parameter of `dequeue`. |
| `MaxQueueLength` | `0` | Maximum number of messages in a single queue. |

## Long polling

Clients can wait for changes instead of polling in a loop. Every response contains a `hashCode` of the requested
queues. A client sends it back in the header `X-MQ-HashCode`; the server then holds the request until the queues
change or the polling time is over.

| Setting / header | Default | Description |
|---|---|---|
| `MessageQueue:MaxRequestPollingSeconds` | `60` | Maximum time a request is held |
| `X-MQ-MaxPollingSeconds` (header) | | Shorter polling time for a single request (limited by the setting above) |

The [.NET client](../client.md) and the [patterns](../patterns/queue-worker.md) use long polling automatically.

## Authorization

Without an `Authorization` section, the queue is **open to everybody** (a warning is logged at startup).
For production, configure `Basic` authentication (currently the only supported type) and list the clients under
`Authorization:Clients`.

Clients send their credentials as HTTP header (`Authorization: Basic base64(id:secret)`) or in URL format
(`https://clientId:clientSecret@my-server`).

!!! warning "Use HTTPS"
    Basic authentication transfers the credentials in plain text. Use HTTPS, directly or through a TLS terminating
    reverse proxy.

When authentication is enabled, every request needs valid credentials, including `/info`, the OpenAPI document and
the Scalar UI. Only the health check endpoints `/health` and `/alive` are anonymous.

### Secrets

Secrets can be stored in plain text or as SHA-256 hash: `"Secret": "sha256:<64 hex characters>"`.

=== "Linux"

    ```bash
    printf '%s' 'my-secret' | sha256sum
    ```

=== "PowerShell"

    ```powershell
    [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes('my-secret')))
    ```

### Roles

Roles are hierarchical: `admin` includes `write`, `write` includes `read`.
A client without `Roles` gets the `admin` role (backward compatibility).

| Role | Allowed methods |
|---|---|
| `read` | `info`, `all`, `length`, `properties`, `queuenames` |
| `write` | `read` + `enqueue`, `dequeue`, `confirmdequeue`, `register`/`remove` for a single queue (no pattern) |
| `admin` | `write` + `register`/`remove` with patterns, `deletemessage` |

Missing or invalid credentials return `401`, insufficient roles return `403`.

With authentication enabled, only the client that dequeued a message can confirm it.

## OpenAPI

| Setting | Description |
|---|---|
| `openApiUI` (env `OPENAPIUI`) | `true` enables `/openapi/v1.json` and the Scalar UI at `/scalar` outside the `Development` environment. The old setting `swaggerUI` still works as a fallback. |

## Errors

Methods return `success: false` and an `errorMessage` on errors. Only expected errors (e.g. invalid queue name,
limit exceeded, queue suspended) return a descriptive message. Unexpected errors return `Internal server error`;
the details are written to the log.
