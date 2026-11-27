# Installation

MessageQueue.NET consists of two web applications:

- **MessageQueueNET**: the REST API (the actual message queue)
- **MessageQueueNET.Blazor**: the dashboard (optional)

Both run as Docker containers, standalone (`dotnet MessageQueueNET.dll`), behind *IIS* or as resources in an
[Aspire](https://aspire.dev) application.

## Docker

The images are published to the GitHub Container Registry. They run as the non-root user `app` on port `8080`.
Tags: `X.Y.Z`, `X.Y` and `latest` (preview versions only get their version tag).

=== "docker compose"

    [`deploy/docker/docker-compose.yml`](https://github.com/jugstalt/MessageQueueNET/blob/master/deploy/docker/docker-compose.yml) starts the REST API (port `9091`) with Basic
    authentication and persisted queues, and the dashboard (port `9090`, only on `127.0.0.1`: it has no login of
    its own). Two clients are configured: `app` (role `write`) for your applications and `dashboard` (role `read`).

    1. Copy [`deploy/docker/.env.example`](https://github.com/jugstalt/MessageQueueNET/blob/master/deploy/docker/.env.example) to `deploy/docker/.env` (ignored by git) and fill in the
       secrets. The comments in the file show how to create a random secret and its `sha256:` hash.
       `MQ_DASHBOARD_CLIENT_SECRET` (plain text, the dashboard sends it) and `MQ_DASHBOARD_CLIENT_SECRET_HASH`
       (stored by the server) must belong together.
    2. Start:

        ```bash
        docker compose -f deploy/docker/docker-compose.yml up -d
        ```

    Applications connect with `http://app:<secret>@<host>:9091`. `MQ_DASHBOARD_ROLE=write` or `admin` lets the
    dashboard create, suspend and delete queues; `MQ_OPENAPIUI=true` enables `/openapi/v1.json` and `/scalar`;
    `MQ_TAG` selects the image version.

=== "docker run"

    ```bash
    docker run -d -p 9091:8080 -v mq-data:/home/app/messagequeue \
      -e MESSAGEQUEUE__PERSIST__TYPE=filesystem \
      -e MESSAGEQUEUE__PERSIST__ROOTPATH=/home/app/messagequeue/persist \
      ghcr.io/jugstalt/messagequeue-net

    docker run -d -p 9090:8080 \
      -e DASHBOARD__QUEUES__0__NAME=dev \
      -e DASHBOARD__QUEUES__0__URL=http://host.docker.internal:9091 \
      ghcr.io/jugstalt/messagequeue-net-dashboard
    ```

!!! tip "Persistence"
    Persisted data belongs in `/home/app/messagequeue`. Mount a volume there, otherwise the queues are lost
    when the container is recreated.

## Aspire

The NuGet package [`MessageQueueNET.Aspire.Hosting`](https://www.nuget.org/packages/MessageQueueNET.Aspire.Hosting/)
adds the server and the dashboard as container resources to an Aspire AppHost:

```csharp title="AppHost/Program.cs"
var builder = DistributedApplication.CreateBuilder(args);

var mq = builder
    .AddMessageQueueNET("messagequeue")
    .WithBindMountPersistance() // (1)!
    .Build();

builder
    .AddDashboardForMessageQueueNET("messagequeue-dashboard")
    .ConnectToMessageQueue(mq, "mq")               // (2)!
    .ConnectToMessageQueue(mq, "mail", "mail*")    // (3)!
    .WithMaxPollingSeconds(5)
    .Build();

builder.Build().Run();
```

1. Persists the queues to `%LOCALAPPDATA%/messagequeue-aspire`. Use `WithVolumePersistance()` for a volume.
2. Shows all queues of the server under the name `mq`.
3. A second view on the same server, filtered to the queues `mail*`.

`AddMessageQueueNET` and `AddDashboardForMessageQueueNET` accept optional `httpPort` and `imageTag` arguments.

## ZIP packages

Every [GitHub release](https://github.com/jugstalt/MessageQueueNET/releases) contains ZIP packages for `win-x64`
and `linux-x64`. They require the [ASP.NET Core 10 runtime](https://dotnet.microsoft.com/download/dotnet/10.0).

```text
<install root>/                               # e.g. C:\apps\messagequeue
├── data/persist/                             # persisted queues (Windows: created on the first start)
└── <version>/                                # content of the ZIP
    ├── api/app/                                  # MessageQueueNET (REST API)
    ├── dashboard/app/                            # MessageQueueNET.Blazor (dashboard)
    ├── cmd/                                      # MessageQueueNET.Cmd, MessageQueueNET.Processor
    ├── start-messagequeue(.bat|.sh)              # API on http://localhost:9091
    └── start-messagequeue-dashboard(.bat|.sh)    # dashboard on http://localhost:9090
```

To run the API in *IIS*, point a website to `api/app` (with the
[ASP.NET Core Hosting Bundle](https://learn.microsoft.com/aspnet/core/host-and-deploy/iis/hosting-bundle) installed).

!!! note "Updates"
    Unpack the ZIP into a folder like `C:\apps\messagequeue`; on the first start on Windows the API stores the
    queues in `data/persist` next to the version folders (`C:\apps\messagequeue\data\persist`), so they survive
    an update. Put your settings into `_config/message-queue.json` (API) and `_config/dashboard.json` (dashboard).
    To update, unpack the new version next to the old one and copy both `_config` folders into it.

## Dashboard

The dashboard reads the section `Dashboard` from `appsettings.json`, `_config/dashboard.json` or environment
variables (`DASHBOARD__…`):

```json title="_config/dashboard.json"
{
  "Dashboard": {
    "Queues": [
      { "Name": "production", "Url": "https://dashboard:secret@mq.example.com" },
      { "Name": "mail", "Url": "https://dashboard:secret@mq.example.com", "Filter": "mail.*" }
    ],
    "MaxPollingSeconds": 10
  }
}
```

On the first start without any configured server (e.g. from the ZIP download), the dashboard creates
`_config/dashboard.json` with the REST API on `http://localhost:9091`. Edit this file for your servers.

| Setting | Description |
|---|---|
| `Queues[].Name` | Display name of the server |
| `Queues[].Url` | URL of the REST API, credentials in URL format (`https://clientId:secret@host`) |
| `Queues[].Filter` | Queue name pattern (default: `*`) |
| `MaxPollingSeconds` | Maximum long polling time of the dashboard requests |

!!! tip
    Give the dashboard a client with the `read` role. It needs `write` or `admin` only to create, suspend
    or delete queues from the UI. See [Authorization](api/configuration.md#authorization).

## OpenAPI and Scalar

In the `Development` environment, or if `openApiUI` is `true` (environment variable `OPENAPIUI`), the API
description is available at `/openapi/v1.json` and an interactive UI at `/scalar`.

## .NET tool

The command line client is also available as a .NET tool (requires the .NET 10 runtime):

```bash
dotnet tool install -g MessageQueueNET.Cmd
mqnet http://localhost:9091 -c shell
```

See [Command line tools](tools.md).
