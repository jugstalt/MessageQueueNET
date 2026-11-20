# Command line tools

## mqnet

`MessageQueueNET.Cmd` sends single commands to a queue or opens an interactive shell. It is part of the ZIP
packages (folder `cmd`) and is available as .NET tool `mqnet`. Both require the .NET 10 runtime.

```bash
dotnet tool install -g MessageQueueNET.Cmd
```

### Single commands

```text
mqnet serviceUrl -q queueName -c command {-m message | -f messages-file}
      command: remove, enqueue, dequeue, length, queuenames, register, properties, all, shell
```

- `serviceUrl`, `-q` and `-c` are required. With [authorization](api/configuration.md#authorization), pass the
  credentials in the URL: `https://clientId:clientSecret@my-server`.
- `-m` adds a message; it can be used several times.
- `-f` reads messages from a text file. Each line is one message, blank lines are ignored.

The commands correspond to the [REST methods](api/rest.md).

```bash
mqnet http://localhost:9091 -q my-queue-1 -c enqueue -m "Message 1"
```

### Shell

The command `shell` starts an interactive shell. Type `help` to list all commands and options.

```bash
mqnet http://localhost:9091 -c shell
```

```text
MessageQueueNET Shell
Type help for help...
>> queue1 register -confirmationperiod 60
>> queue1 properties
LastAccessUTC: 10.01.2022 13:59:03
Length: 0
LifetimeSeconds: 0
ItemLifetimeSeconds: 0
SuspendEnqueue: False
SuspendDequeue: False
>> queuenames
queue1
>> queue1 enqueue -m "message #1" -m "message #2"
>> queue1 all
message #1
message #2
>> exit
```

| Command | Options |
|---|---|
| `register` | `-lifetime n`, `-itemlifetime n`, `-confirmationperiod n`, `-maxunconfirmeditems n`, `-suspend-enqueue true\|false`, `-suspend-dequeue true\|false` |
| `properties` | |
| `remove` | |
| `length` | `-full`: include unconfirmed messages |
| `all` | `-max n`, `-unconfirmed`, `-full`: show id and message |
| `enqueue` | `-m message` (several times), `-f file`, `-ping`, `-workercmd command` |
| `dequeue` | |
| `confirm` | `-id messageId` |

## MessageQueueNET.Processor

`MessageQueueNET.Processor` is a console application that watches queues and executes command line jobs from
the messages. It uses the [queue worker pattern](patterns/queue-worker.md) with the worker `mq.commandline`.

```powershell
.\MessageQueueNET.Processor.exe `
    --api https://localhost:5001 `
    --filter mq-processor.* `
    --output c:\temp\mq-processor `
    --command c:\temp\*.exe,c:\temp\*.cmd,c:\temp\*.bat
```

| Parameter | Description |
|---|---|
| `--api` | URL of the MessageQueue.NET API |
| `--filter` | Queue name or [pattern](api/rest.md#patterns) to watch |
| `--output` | Directory for the result logs |
| `--command` | Comma-separated list of allowed commands (wildcards allowed) |

!!! danger "Security"
    Only commands matching `--command` are executed. All other messages are rejected, so arbitrary processes
    cannot be started through the queue. Keep this list as small as possible.

The result of every job is written to the output directory.

### Message format

```json
{
  "Body": {
    "Command": "c:\\temp\\test.bat",
    "Arguments": "123"
  },
  "ProcessId": "123",
  "Worker": "mq.commandline",
  "ResultQueue": "mq-processor.jobs.results",
  "Publisher": "username",
  "Subject": "executing test.bat ..."
}
```

- `ProcessId`: unique identifier of the job.
- `Worker`: always `mq.commandline`.
- `Body.Command`, `Body.Arguments`: command and arguments. The command must match `--command`.
- `ResultQueue`, `Publisher`, `Subject`: optional.

### Sending jobs with mqnet

In the [shell](#shell), `-workercmd` creates messages in this format. Each `-m` value (or each line of `-f`) becomes
the arguments of one job:

```text
>> mq-processor.jobs enqueue -workercmd c:\temp\test.bat -m "argument1 argument2"
>> mq-processor.jobs enqueue -workercmd c:\temp\test.bat -f c:\temp\arguments.txt
```
