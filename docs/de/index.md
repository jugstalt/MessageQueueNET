# MessageQueue.NET

**MessageQueue.NET** ist eine schlanke Message Queue mit REST-API, geschrieben in C# auf **.NET 10**.
Sender und Empfänger kommunizieren nie direkt miteinander: Eine Nachricht bleibt in einer Queue, bis ein
Empfänger sie abholt, und jede Nachricht wird von genau einem Empfänger verarbeitet.

<div class="grid cards" markdown>

-   :material-api:{ .lg .middle } **Web-API**

    ---

    Queues über ihren Namen anlegen, Nachrichten einreihen und abholen, Long Polling, Bestätigungen und Limits.

    [:octicons-arrow-right-24: REST-Referenz](api/rest.md)

-   :material-language-csharp:{ .lg .middle } **.NET-Client**

    ---

    `MessageQueueNET.Client` kapselt die REST-API und bringt die Patterns Queue Worker und App Topic mit.

    [:octicons-arrow-right-24: Client-Bibliothek](client.md)

-   :material-console:{ .lg .middle } **Kommandozeilen-Tools**

    ---

    Die `mqnet`-Shell zum Ansehen und Befüllen von Queues, `MessageQueueNET.Processor` zum Ausführen von Befehlen aus einer Queue.

    [:octicons-arrow-right-24: Tools](tools.md)

-   :material-monitor-dashboard:{ .lg .middle } **Dashboard**

    ---

    Blazor-Dashboard zum Beobachten von Queues, Durchsuchen von Nachrichten und Verwalten von Queue-Eigenschaften.

    [:octicons-arrow-right-24: Installation](installation.md)

</div>

## Überblick

![Architektur von MessageQueue.NET](../images/architecture.svg){ .mq-diagram }

- **Producer** (Anwendungen, das Kommandozeilen-Tool) legen Nachrichten in benannte Queues.
- **MessageQueue.NET** hält die Queues im Speicher und speichert sie optional im Dateisystem.
- **Consumer** (Worker, App-Instanzen, der Processor) holen Nachrichten ab. Mit einer *Confirmation Period*
  kommt eine Nachricht, die nicht rechtzeitig bestätigt wird, zurück in die Queue.
- Das **Dashboard** zeigt den Zustand eines oder mehrerer MessageQueue.NET-Server.

## Funktionen

- Beliebig viele Queues, angesprochen über den Namen oder ein Muster (`orders.*`, `queue1,queue2`)
- **Long Polling**: Consumer warten auf neue Nachrichten, statt in einer Schleife abzufragen
- **Bestätigungen**: Nachrichten kommen zurück in die Queue, wenn die Verarbeitung nicht rechtzeitig bestätigt wird
- Lebensdauer für Queues und Nachrichten, Enqueue/Dequeue aussetzen, Limits gegen Ressourcenerschöpfung
- Queues im Speicher mit optionaler **Persistierung im Dateisystem**
- **Basic Authentication** mit den Rollen `read`, `write` und `admin`
- OpenAPI-Beschreibung und [Scalar](https://github.com/scalar/scalar)-UI
- Docker-Images, ZIP-Pakete, NuGet-Pakete, ein .NET-Tool und eine [Aspire](https://aspire.dev)-Integration

## Pakete

| Paket | Beschreibung |
|---|---|
| [`MessageQueueNET.Client`](https://www.nuget.org/packages/MessageQueueNET.Client/) | .NET-Client, Services für Queue Worker und App Topic |
| [`MessageQueueNET.Core`](https://www.nuget.org/packages/MessageQueueNET.Core/) | Gemeinsame Modelle und Abstraktionen |
| [`MessageQueueNET.Worker`](https://www.nuget.org/packages/MessageQueueNET.Worker/) | Fertige Kommandozeilen-Worker (von `MessageQueueNET.Processor` verwendet) |
| [`MessageQueueNET.Aspire.Hosting`](https://www.nuget.org/packages/MessageQueueNET.Aspire.Hosting/) | Aspire-Ressourcen für Server und Dashboard |
| [`MessageQueueNET.Cmd`](https://www.nuget.org/packages/MessageQueueNET.Cmd/) | .NET-Tool `mqnet` |
| `ghcr.io/jugstalt/messagequeue-net` | Docker-Image der REST-API |
| `ghcr.io/jugstalt/messagequeue-net-dashboard` | Docker-Image des Dashboards |

## Nächste Schritte

1. Server und Dashboard [installieren](installation.md).
2. Persistierung, Limits und Authentifizierung [konfigurieren](api/configuration.md).
3. Die [REST-API](api/rest.md) oder den [.NET-Client](client.md) verwenden.
4. Worker mit dem [Queue-Worker-Pattern](patterns/queue-worker.md) bauen oder App-Instanzen mit dem
   [App-Topic-Pattern](patterns/app-topic.md) synchron halten.
