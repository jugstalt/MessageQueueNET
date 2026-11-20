# MessageQueue.NET

[![CI](https://github.com/jugstalt/MessageQueueNET/actions/workflows/ci.yml/badge.svg)](https://github.com/jugstalt/MessageQueueNET/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/MessageQueueNET.Client?label=MessageQueueNET.Client&logo=nuget)](https://www.nuget.org/packages/MessageQueueNET.Client/)
[![Docker](https://img.shields.io/badge/ghcr.io-messagequeue--net-2496ED?logo=docker&logoColor=white)](https://github.com/jugstalt/MessageQueueNET/pkgs/container/messagequeue-net)
[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/github/license/jugstalt/MessageQueueNET)](https://github.com/jugstalt/MessageQueueNET/blob/master/LICENSE)
[![Docs](https://img.shields.io/badge/docs-docs.gviewonline.com-blue)](https://docs.gviewonline.com/other/messagequeue-net/index.html)

**A lightweight message queue with a REST API, written in C# on .NET 10.** Senders and receivers never talk to
each other directly: a message stays in a queue until one receiver picks it up.

- Any number of queues, addressed by name or pattern (`orders.*`)
- Long polling, confirmations with automatic retry, queue and message lifetimes, limits
- In-memory queues with optional file system persistence
- Basic authentication with `read` / `write` / `admin` roles
- .NET client with *queue worker* and *app topic* patterns, `mqnet` command line shell, Blazor dashboard
- Docker images, ZIP packages and an [Aspire](https://aspire.dev) integration

## Quickstart

```bash
curl -O https://raw.githubusercontent.com/jugstalt/MessageQueueNET/master/deploy/docker/docker-compose.yml
docker compose up -d
```

The API runs on `http://localhost:9091`, the dashboard on `http://localhost:9090`.
With Aspire: `builder.AddMessageQueueNET("messagequeue")` (package `MessageQueueNET.Aspire.Hosting`).

Enqueue and dequeue a message:

```bash
curl -X PUT http://localhost:9091/Queue/enqueue/orders -H "Content-Type: application/json" -d '["order #1"]'
curl -X POST "http://localhost:9091/Queue/dequeue/orders?count=1"
```

From .NET:

```bash
dotnet add package MessageQueueNET.Client
```

```csharp
var client = new QueueClient(new MessageQueueConnection("http://localhost:9091"), "orders");

await client.EnqueueAsync(["order #1"]);

var result = await client.DequeueAsync(count: 1);
```

## Packages

| Package | Description |
|---|---|
| [MessageQueueNET.Client](https://www.nuget.org/packages/MessageQueueNET.Client/) | .NET client, queue worker and app topic services |
| [MessageQueueNET.Core](https://www.nuget.org/packages/MessageQueueNET.Core/) | Shared models and abstractions |
| [MessageQueueNET.Worker](https://www.nuget.org/packages/MessageQueueNET.Worker/) | Ready-made command line workers |
| [MessageQueueNET.Aspire.Hosting](https://www.nuget.org/packages/MessageQueueNET.Aspire.Hosting/) | Aspire resources for server and dashboard |
| [MessageQueueNET.Cmd](https://www.nuget.org/packages/MessageQueueNET.Cmd/) | .NET tool `mqnet` |
| [ghcr.io/jugstalt/messagequeue-net](https://github.com/jugstalt/MessageQueueNET/pkgs/container/messagequeue-net) | Docker image of the REST API |
| [ghcr.io/jugstalt/messagequeue-net-dashboard](https://github.com/jugstalt/MessageQueueNET/pkgs/container/messagequeue-net-dashboard) | Docker image of the dashboard |

ZIP packages for `win-x64` and `linux-x64` are attached to every [GitHub release](https://github.com/jugstalt/MessageQueueNET/releases).

## Documentation

📖 **[docs.gviewonline.com](https://docs.gviewonline.com/other/messagequeue-net/index.html)** – English and German

[Installation](https://docs.gviewonline.com/other/messagequeue-net/installation.html) ·
[Configuration](https://docs.gviewonline.com/other/messagequeue-net/api/configuration.html) ·
[REST API](https://docs.gviewonline.com/other/messagequeue-net/api/rest.html) ·
[.NET client](https://docs.gviewonline.com/other/messagequeue-net/client.html) ·
[Queue worker](https://docs.gviewonline.com/other/messagequeue-net/patterns/queue-worker.html) ·
[App topic](https://docs.gviewonline.com/other/messagequeue-net/patterns/app-topic.html) ·
[Tools](https://docs.gviewonline.com/other/messagequeue-net/tools.html) ·
[Changelog](https://github.com/jugstalt/MessageQueueNET/blob/master/CHANGELOG.md)

Build the docs locally with `./build.ps1 Docs` (output in `artifacts/docs/site`).

## License

[Apache License 2.0](https://github.com/jugstalt/MessageQueueNET/blob/master/LICENSE)
