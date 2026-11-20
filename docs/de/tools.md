# Kommandozeilen-Tools

## mqnet

`MessageQueueNET.Cmd` schickt einzelne Befehle an eine Queue oder öffnet eine interaktive Shell. Das Tool ist Teil
der ZIP-Pakete (Ordner `cmd`) und als .NET-Tool `mqnet` verfügbar. Beides benötigt die .NET 10 Runtime.

```bash
dotnet tool install -g MessageQueueNET.Cmd
```

### Einzelbefehle

```text
mqnet serviceUrl -q queueName -c command {-m message | -f messages-file}
      command: remove, enqueue, dequeue, length, queuenames, register, properties, all, shell
```

- `serviceUrl`, `-q` und `-c` sind Pflicht. Mit [Autorisierung](api/configuration.md#autorisierung) die
  Zugangsdaten in der URL übergeben: `https://clientId:clientSecret@my-server`.
- `-m` fügt eine Nachricht hinzu und kann mehrfach angegeben werden.
- `-f` liest Nachrichten aus einer Textdatei. Jede Zeile ist eine Nachricht, leere Zeilen werden ignoriert.

Die Befehle entsprechen den [REST-Methoden](api/rest.md).

```bash
mqnet http://localhost:9091 -q my-queue-1 -c enqueue -m "Message 1"
```

### Shell

Der Befehl `shell` startet eine interaktive Shell. `help` listet alle Befehle und Optionen auf.

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

| Befehl | Optionen |
|---|---|
| `register` | `-lifetime n`, `-itemlifetime n`, `-confirmationperiod n`, `-maxunconfirmeditems n`, `-suspend-enqueue true\|false`, `-suspend-dequeue true\|false` |
| `properties` | |
| `remove` | |
| `length` | `-full`: unbestätigte Nachrichten mitzählen |
| `all` | `-max n`, `-unconfirmed`, `-full`: Id und Nachricht anzeigen |
| `enqueue` | `-m message` (mehrfach), `-f file`, `-ping`, `-workercmd command` |
| `dequeue` | |
| `confirm` | `-id messageId` |

## MessageQueueNET.Processor

`MessageQueueNET.Processor` ist eine Konsolenanwendung, die Queues beobachtet und Kommandozeilen-Jobs aus den
Nachrichten ausführt. Sie verwendet das [Queue-Worker-Pattern](patterns/queue-worker.md) mit dem Worker `mq.commandline`.

```powershell
.\MessageQueueNET.Processor.exe `
    --api https://localhost:5001 `
    --filter mq-processor.* `
    --output c:\temp\mq-processor `
    --command c:\temp\*.exe,c:\temp\*.cmd,c:\temp\*.bat
```

| Parameter | Beschreibung |
|---|---|
| `--api` | URL der MessageQueue.NET-API |
| `--filter` | Queue-Name oder [Muster](api/rest.md#patterns), das beobachtet wird |
| `--output` | Verzeichnis für die Ergebnis-Logs |
| `--command` | Kommagetrennte Liste erlaubter Befehle (Platzhalter erlaubt) |

!!! danger "Sicherheit"
    Nur Befehle, die zu `--command` passen, werden ausgeführt. Alle anderen Nachrichten werden abgelehnt, damit
    über die Queue keine beliebigen Prozesse gestartet werden können. Diese Liste so klein wie möglich halten.

Das Ergebnis jedes Jobs wird in das Ausgabeverzeichnis geschrieben.

### Nachrichtenformat

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

- `ProcessId`: eindeutige Kennung des Jobs.
- `Worker`: immer `mq.commandline`.
- `Body.Command`, `Body.Arguments`: Befehl und Argumente. Der Befehl muss zu `--command` passen.
- `ResultQueue`, `Publisher`, `Subject`: optional.

### Jobs mit mqnet senden

In der [Shell](#shell) erzeugt `-workercmd` Nachrichten in diesem Format. Jeder `-m`-Wert (oder jede Zeile aus
`-f`) wird zu den Argumenten eines Jobs:

```text
>> mq-processor.jobs enqueue -workercmd c:\temp\test.bat -m "argument1 argument2"
>> mq-processor.jobs enqueue -workercmd c:\temp\test.bat -f c:\temp\arguments.txt
```
