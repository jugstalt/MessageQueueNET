# Commandline Tools

The software package provides command line tools that allow access to the queues. The tools are part of the ZIP packages (folder `cmd`, single files, require the .NET 10 runtime).

`MessageQueueNET.Cmd` is also available as .NET tool (requires the .NET 10 runtime):

```
dotnet tool install -g MessageQueueNET.Cmd
mqnet http://my-queue-server -c shell
```

In the examples below, `mqnet` can be used instead of `MessageQueueNET.Cmd.exe`.

## MessageQueueNET.Cmd.exe

This tool can be used to pass individual commands to the queue (e.g. insert a new message)
Use it as follows:

```
Usage: MessageQueueNET.Cmd.exe serviceUrl -q queueName -c comand {-m message | -f messages-file}
       command: remove, enqueue, dequeue, length, queuenames, register, all
```

`serviceUrl`, `queneName` and `command` are required. If you use the API authorization, it can be done using url-format (`https://clientId:clientSecret@my-server/...`).

If messages are added to a queue, this can be done with the `-m` option. If multiple messages are passed at the same time,
you can pass a text file with the messages with the `-f` option. Each line in the text file corresponds to a message. Blank lines are ignored.

The above values can be specified as commands after the `-c` option. The meaning of each command was described in the REST API documentation.

Example

```
./MessageQueueNET.Cmd.exe http://my-queue-server -q my-queue-1 -c enqueue -m "Message 1"
```

### MessageQueueNET.Cmd Shell

The 'shell' command starts a simple shell. With the shell you can send commands to a MessageQueueNET instance.

```
./MessageQueueNET.Cmd.exe http://my-queue-server -c shell
```

Example:

```
MessageQueueNET Shell
Type help for help...
>>
>> queue1 register
>> queue1 properties
LastAccessUTC: 10.01.2022 13:59:03
Length: 0
LifetimeSeconds: 0
ItemLifetimeSeconds: 0
SuspendEnqueue: False
SuspendDequeue: False
>>
>> queuenames
queue1
>>
>> queue1 enqueue -m "message #1" -m "message #2"
>>
>> queue1 all
message #1
message #2
>>
>> exit
```


## MessageQueueNET.Processor

MessageQueueNET.Processor is a C# console application designed to monitor a message queue and execute the listed messages. It's a robust tool for managing and processing tasks queued in your system.

### How It Works

The application continuously monitors a specified message queue. When a new message appears, it executes the command associated with that message, adhering to predefined filters and constraints for security and efficiency.

### Usage

Run the application using the following command:

```
.\MessageQueueNET.Processor.exe --api <API_URL> --filter <QUEUE_NAME> --output <LOG_DIRECTORY> --command <ALLOWED_COMMANDS>
```

Example:
```
.\MessageQueueNET.Processor.exe --api https://localhost:5001 --filter mq-processor.* --output c:\temp\mq-processor --command c:\temp\*.exe,c:\temp\*.cmd,c:\temp\*.bat
```

### Parameters

 * `--api` URL to the MessageQueue.
 * `--filter` Name of the queue to monitor. Wildcards can be used to monitor multiple queues.
 * `--output` Directory where logs for the results will be stored.
 * `--command` A comma-separated list of commands that are allowed to be executed. Wildcards can be used. This list must be specified to ensure that only specific, safe processes are started.

### Message Format in the Queue

Messages in the queue must be structured in a specific format to be processed correctly. Here's an example of a valid message:

``` javascript
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

Required Properties

* `ProcessId`: A unique identifier for the process.
* `Worker`: Specifies the worker that will execute the message. This must always be set to "mq.commandline".
* `Command`, Arguments: The command to be executed along with its parameters. The command must match one of the allowed commands specified in the --command parameter; otherwise, it will be rejected for security reasons.

`Publisher` and `Subject` are optional Properties

### Security

The application ensures that only specified commands can be run, preventing the execution of arbitrary or potentially harmful processes. This feature is critical for maintaining the security and integrity of your system.

### Logs

Logs for each processed message and the result of its execution are stored in the specified output directory. This ensures easy tracking and auditing of the tasks handled by the application.

### Sending Messages to the Queue Using Command Line

To send messages to the queue in the format required by MessageQueueNET.Processor, you can use the MessageQueueNET.Cmd command line tool. Follow these steps to enqueue messages:

1. Run the Command Line Tool

Execute the following command to start the MessageQueueNET.Cmd tool:

```
.\MessageQueueNET.Cmd.exe <url-to-messagequeue> -c shell
```

Replace <url-to-messagequeue> with the URL of your message queue.

2. Enqueue a Message

Once in the shell, use the following command to enqueue a message:

```
<name-of-the-queue> enqueue -workercmd c:\temp\test.bat -m "argument1 argument2..."
```

Replace <name-of-the-queue> with the name of your queue, and adjust the command and arguments as required.

Optional: Enqueue Messages from a File

Alternatively, instead of manually typing out each message, you can specify a text file containing the parameters/arguments for the command. Each line in the file should contain the arguments for one command. Use the following command to enqueue messages from a file:

```
<name-of-the-queue> enqueue -workercmd c:\temp\test.bat -f <path-to-your-text-file>
```

Replace <path-to-your-text-file> with the path to your text file.

[Queue Worker Pattern](../queue-worker/queue-worker_en.md)