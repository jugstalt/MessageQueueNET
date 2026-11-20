# Installation

MessageQueue.NET besteht aus zwei Webanwendungen:

- **MessageQueueNET**: die REST-API (die eigentliche Message Queue)
- **MessageQueueNET.Blazor**: das Dashboard (optional)

Beide laufen als Docker-Container, eigenständig (`dotnet MessageQueueNET.dll`), hinter dem *IIS* oder als
Ressourcen in einer [Aspire](https://aspire.dev)-Anwendung.

## Docker

Die Images werden in der GitHub Container Registry veröffentlicht. Sie laufen als Benutzer `app` (nicht root)
auf Port `8080`. Tags: `X.Y.Z`, `X.Y` und `latest` (Preview-Versionen bekommen nur ihren Versions-Tag).

=== "docker compose"

    Das Repository enthält ein vollständiges Beispiel in
    [`deploy/docker/docker-compose.yml`](https://github.com/jugstalt/MessageQueueNET/blob/master/deploy/docker/docker-compose.yml):

    ```yaml title="docker-compose.yml"
    services:
      messagequeue:
        image: ghcr.io/jugstalt/messagequeue-net:${MQ_TAG:-latest}
        ports:
          - "9091:8080" # (1)!
        environment:
          MESSAGEQUEUE__PERSIST__TYPE: filesystem # (2)!
          MESSAGEQUEUE__PERSIST__ROOTPATH: /home/app/messagequeue/persist
          OPENAPIUI: "true" # (3)!
        volumes:
          - messagequeue-data:/home/app/messagequeue
        restart: unless-stopped

      dashboard:
        image: ghcr.io/jugstalt/messagequeue-net-dashboard:${MQ_TAG:-latest}
        ports:
          - "9090:8080"
        environment:
          DASHBOARD__QUEUES__0__NAME: dev # (4)!
          DASHBOARD__QUEUES__0__URL: http://messagequeue:8080
        depends_on:
          - messagequeue
        restart: unless-stopped

    volumes:
      messagequeue-data:
    ```

    1. Die REST-API ist unter `http://localhost:9091` erreichbar.
    2. Speichert die Queues auf der Platte, damit sie einen Neustart überleben. Ohne diese Einstellung leben
       die Queues nur im Speicher.
    3. Aktiviert `/openapi/v1.json` und die Scalar-UI unter `/scalar`.
    4. Das Dashboard kann mehrere Server anzeigen: `DASHBOARD__QUEUES__1__…` fügt den nächsten hinzu.

    ```bash
    docker compose -f deploy/docker/docker-compose.yml up -d
    ```

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

!!! tip "Persistierung"
    Persistierte Daten gehören nach `/home/app/messagequeue`. Dort ein Volume einhängen, sonst gehen die Queues
    verloren, wenn der Container neu erstellt wird.

## Aspire

Das NuGet-Paket [`MessageQueueNET.Aspire.Hosting`](https://www.nuget.org/packages/MessageQueueNET.Aspire.Hosting/)
fügt Server und Dashboard als Container-Ressourcen zu einem Aspire-AppHost hinzu:

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

1. Speichert die Queues unter `%LOCALAPPDATA%/messagequeue-aspire`. Für ein Volume `WithVolumePersistance()` verwenden.
2. Zeigt alle Queues des Servers unter dem Namen `mq`.
3. Eine zweite Sicht auf denselben Server, gefiltert auf die Queues `mail*`.

`AddMessageQueueNET` und `AddDashboardForMessageQueueNET` haben die optionalen Argumente `httpPort` und `imageTag`.

## ZIP-Pakete

Jedes [GitHub-Release](https://github.com/jugstalt/MessageQueueNET/releases) enthält ZIP-Pakete für `win-x64`
und `linux-x64`. Sie benötigen die [ASP.NET Core 10 Runtime](https://dotnet.microsoft.com/download/dotnet/10.0).

```text
<version>/
├── api/app/                                  # MessageQueueNET (REST-API)
├── dashboard/app/                            # MessageQueueNET.Blazor (Dashboard)
├── cmd/                                      # MessageQueueNET.Cmd, MessageQueueNET.Processor
├── start-messagequeue(.bat|.sh)              # API auf http://localhost:9091
└── start-messagequeue-dashboard(.bat|.sh)    # Dashboard auf http://localhost:9090
```

Um die API im *IIS* zu betreiben, eine Website auf `api/app` zeigen lassen (das
[ASP.NET Core Hosting Bundle](https://learn.microsoft.com/aspnet/core/host-and-deploy/iis/hosting-bundle) muss installiert sein).

!!! note "Updates"
    Eigene Einstellungen gehören in `_config/message-queue.json` (API) und `_config/dashboard.json` (Dashboard).
    Für ein Update alles außer dem Ordner `_config` über die bestehende Installation kopieren.

## Dashboard

Das Dashboard liest den Abschnitt `Dashboard` aus `appsettings.json`, `_config/dashboard.json` oder aus
Umgebungsvariablen (`DASHBOARD__…`):

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

| Einstellung | Beschreibung |
|---|---|
| `Queues[].Name` | Anzeigename des Servers |
| `Queues[].Url` | URL der REST-API, Zugangsdaten im URL-Format (`https://clientId:secret@host`) |
| `Queues[].Filter` | Muster für Queue-Namen (Standard: `*`) |
| `MaxPollingSeconds` | Maximale Long-Polling-Zeit der Dashboard-Anfragen |

!!! tip
    Dem Dashboard einen Client mit der Rolle `read` geben. `write` oder `admin` braucht es nur, um Queues in der
    Oberfläche anzulegen, auszusetzen oder zu löschen. Siehe [Autorisierung](api/configuration.md#autorisierung).

## OpenAPI und Scalar

In der Umgebung `Development` oder wenn `openApiUI` auf `true` steht (Umgebungsvariable `OPENAPIUI`), ist die
API-Beschreibung unter `/openapi/v1.json` und eine interaktive Oberfläche unter `/scalar` erreichbar.

## .NET-Tool

Der Kommandozeilen-Client ist auch als .NET-Tool verfügbar (benötigt die .NET 10 Runtime):

```bash
dotnet tool install -g MessageQueueNET.Cmd
mqnet http://localhost:9091 -c shell
```

Siehe [Kommandozeilen-Tools](tools.md).
