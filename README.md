# MessageQueueNET v4.x

A message queue API fully implemented in .NET Core

Message queues provide an asynchronous communication protocol, where the sender and recipient of messages do not interact with the message queue at the same time.
Messages in the queue are stored only until the recipient retrieves them. After retrieving, a message is deleted again.
Each message is also picked up and processed by a single recipient.

_MessageQueueNET_ provides the following tools for this protocol:

-   Web API: the _Messsage Queue_ Web API Application
-   Client library: A .NET Standard Library to interact with the API
-   Command line tools: Access to the _Message Queue_ via the command line

## Get it

-   ZIP packages (`win-x64`, `linux-x64`): [GitHub releases](https://github.com/jugstalt/MessageQueueNET/releases)
-   Docker images: `ghcr.io/jugstalt/messagequeue-net`, `ghcr.io/jugstalt/messagequeue-net-dashboard` ([docker-compose.yml](./deploy/docker/docker-compose.yml))
-   NuGet packages: `MessageQueueNET.Client`, `MessageQueueNET.Core`, `MessageQueueNET.Worker`, `MessageQueueNET.Aspire.Hosting`
-   .NET tool: `dotnet tool install -g MessageQueueNET.Cmd` (command `mqnet`)

## Documentation

[Web API](./doc/api/api_en.md)

[Client Library](./doc/client/client_en.md)

[Commandline Tools](./doc/console/tools_en.md)

[Queue Worker Pattern](./doc/queue-worker/queue-worker_en.md)

[App Topic Pattern](./doc/app-topic/app_topic_pattern_en.md)

[Build & Release](./doc/release_en.md)

[Changelog](./CHANGELOG.md)
