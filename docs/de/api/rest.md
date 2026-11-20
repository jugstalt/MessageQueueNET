# REST-Referenz

Alle Queue-Methoden liegen unter `/Queue`. Jede Antwort enthält `success` und bei Fehlern `errorMessage`
(siehe [Fehler](configuration.md#fehler)). Antworten der Queue-Methoden enthalten außerdem einen `hashCode`, der
für [Long Polling](configuration.md#long-polling) verwendet werden kann.

| Methode | Route | Rolle | Beschreibung |
|---|---|---|---|
| `GET` | [`/Info`](#info) | `read` | Version der API |
| `PUT` | [`/Queue/enqueue/{id}`](#enqueue) | `write` | Nachrichten in eine Queue legen |
| `POST` | [`/Queue/dequeue/{idPattern}`](#dequeue) | `write` | Nachrichten aus einer oder mehreren Queues abholen |
| `POST` | [`/Queue/confirmdequeue/{id}`](#confirm-dequeue) | `write` | Verarbeitete Nachricht bestätigen |
| `GET` | [`/Queue/all/{idPattern}`](#all-messages) | `read` | Nachrichten lesen, ohne sie zu entfernen |
| `GET` | [`/Queue/length/{idPattern}`](#length) | `read` | Anzahl der Nachrichten |
| `PUT` | [`/Queue/register/{idPattern}`](#register) | `write`/`admin` | Queue anlegen oder ihre Eigenschaften ändern |
| `GET` | [`/Queue/properties/{idPattern}`](#properties) | `read` | Eigenschaften und Zustand von Queues |
| `GET` | [`/Queue/queuenames`](#queue-names-list) | `read` | Namen aller Queues |
| `DELETE` | [`/Queue/remove/{idPattern}`](#remove) | `write`/`admin` | Queues oder ihre Nachrichten entfernen |
| `DELETE` | [`/Queue/deletemessage/{id}`](#delete-message) | `admin` | Einzelne Nachricht löschen |

Methoden mit `idPattern` brauchen die Rolle `admin`, wenn das Muster mehr als eine Queue trifft
(`register`, `remove`). Siehe [Rollen](configuration.md#rollen).

## Queue-Namen { #queue-names }

Eine Queue wird über einen eindeutigen Namen (`id`) angesprochen. Namen dürfen Kleinbuchstaben, Ziffern, `_`, `-`
und `.` enthalten:

- ein Name beginnt mit einem Kleinbuchstaben oder einer Ziffer,
- auf einen Punkt muss ein Buchstabe, eine Ziffer, `_` oder `-` folgen (kein `..`, kein Punkt am Ende),
- ein Name hat höchstens 128 Zeichen.

Gültig: `queue1`, `test.queue`, `appname.main.in`, `my_queue-123`.
Ungültig: `.`, `..`, `a..b`, `queue.`, `_queue`.

### Muster { #patterns }

Methoden mit `idPattern` arbeiten auf mehreren Queues gleichzeitig. Ein Muster ist eine kommagetrennte Liste aus
Namen und Platzhalter-Ausdrücken:

| Muster | Trifft |
|---|---|
| `queue1,queue2` | `queue1` und `queue2` |
| `test.*` | alle Queues, die mit `test.` beginnen |
| `queue1,test.*` | `queue1` und alle Queues, die mit `test.` beginnen |

## Info { #info }

`GET /Info` liefert die Version der API.

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

```json title="Antwort"
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

## Enqueue { #enqueue }

`PUT /Queue/enqueue/{id}` legt Nachrichten in eine Queue. Der Body ist ein JSON-Array aus Strings. Eine fehlende
Queue wird angelegt.

Enqueue gilt *ganz oder gar nicht*: Kann eine Nachricht nicht gespeichert werden, wird keine Nachricht eingereiht.

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

```json title="Antwort"
{ "success": true }
```

## Dequeue { #dequeue }

`POST /Queue/dequeue/{idPattern}` holt Nachrichten aus einer oder mehreren Queues ab. Die Nachrichten werden
entfernt und stehen anderen Consumern nicht mehr zur Verfügung.

| Parameter | In | Standard | Beschreibung |
|---|---|---|---|
| `idPattern` | Pfad | | Queue-Name oder [Muster](#patterns) |
| `count` | Query | `1` | Anzahl der Nachrichten (begrenzt durch `MaxDequeueCount`) |
| `register` | Query | `false` | Ohne Wirkung, aus Kompatibilitätsgründen vorhanden |

Existiert eine Queue nicht, wird sie angelegt und `[]` zurückgegeben. Andere Prozesse sehen dann über
[`queuenames`](#queue-names-list), dass jemand diese Queue abarbeitet.

!!! info "Bestätigung"
    Hat die Queue `confirmationPeriodSeconds > 0`, trägt die Nachricht `requireConfirmation: true`. Sie muss
    innerhalb dieser Zeit mit [`confirmdequeue`](#confirm-dequeue) bestätigt werden, sonst kommt sie zurück in die Queue.

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

```json title="Antwort"
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

## Dequeue bestätigen { #confirm-dequeue }

`POST /Queue/confirmdequeue/{id}?messageId={messageId}` bestätigt, dass eine Nachricht verarbeitet wurde. Erst
dann wird die Nachricht endgültig aus der Queue entfernt.

Mit aktivierter Authentifizierung kann nur der Client eine Nachricht bestätigen, der sie abgeholt hat.

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

```json title="Antwort"
{ "success": true }
```

## Alle Nachrichten { #all-messages }

`GET /Queue/all/{idPattern}` liefert die Nachrichten von Queues, ohne sie zu verändern.

| Parameter | In | Standard | Beschreibung |
|---|---|---|---|
| `max` | Query | `0` | Maximale Anzahl an Nachrichten (`0`: alle) |
| `unconfirmedOnly` | Query | `false` | Nur Nachrichten, die abgeholt, aber noch nicht bestätigt wurden (nur Queues mit `confirmationPeriodSeconds > 0`) |

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

Die Antwort hat dasselbe Format wie bei [Dequeue](#dequeue).

## Länge { #length }

`GET /Queue/length/{idPattern}` liefert die Anzahl der Nachrichten pro Queue.

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

```json title="Antwort"
{
  "success": true,
  "queues": {
    "test.queue1": { "queueLength": 1, "unconfirmedItems": 0 }
  }
}
```

## Register { #register }

`PUT /Queue/register/{idPattern}` legt eine Queue an (falls sie nicht existiert) oder ändert die Eigenschaften
bestehender Queues. Alle Parameter sind optionale Query-Parameter; weggelassene Parameter behalten ihren aktuellen Wert.

| Eigenschaft | Typ | Beschreibung |
|---|---|---|
| `lifetimeSeconds` | int | Die Queue wird gelöscht, nachdem sie so lange leer und unbenutzt war. `0`: nie. |
| `itemLifetimeSeconds` | int | Nachrichten, die in dieser Zeit nicht abgeholt werden, werden gelöscht. `0`: nie. |
| `confirmationPeriodSeconds` | int | Abgeholte Nachrichten müssen in dieser Zeit [bestätigt](#confirm-dequeue) werden, sonst kommen sie zurück in die Queue. `0`: keine Bestätigung. |
| `maxUnconfirmedItems` | int | Maximale Anzahl abgeholter, aber unbestätigter Nachrichten. Ist sie erreicht, liefert `dequeue` nichts, bis Nachrichten bestätigt werden. |
| `maxUnconfirmedItemsStrategy` | int | `0` *Absolute*: das Maximum gilt für die Queue. `1` *PerClient*: das Maximum gilt pro Client. |
| `suspendEnqueue` | bool | `true`: es können keine Nachrichten hinzugefügt werden. |
| `suspendDequeue` | bool | `true`: es können keine Nachrichten abgeholt werden. Die Nachrichten bleiben erhalten. |

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

Die Antwort hat dasselbe Format wie bei [Properties](#properties).

## Properties { #properties }

`GET /Queue/properties/{idPattern}` liefert die Eigenschaften und den Zustand von Queues.
Anfragen mit dem Header `X-MQ-SilentAccess: true` aktualisieren `lastAccessUTC` nicht (wird vom Dashboard verwendet).

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

```json title="Antwort"
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

## Queue-Namen (Liste) { #queue-names-list }

`GET /Queue/queuenames` liefert die Namen aller Queues.

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

```json title="Antwort"
{ "success": true, "queueNames": [ "orders", "test.queue1" ] }
```

## Remove { #remove }

`DELETE /Queue/remove/{idPattern}?removeType={removeType}` entfernt Queues oder ihre Nachrichten.

| `removeType` | Beschreibung |
|---|---|
| `0` (Standard) | Queue entfernen |
| `1` | Alle Nachrichten entfernen |
| `2` | Die unbestätigten Nachrichten entfernen |

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

```json title="Antwort"
{ "success": true }
```

## Nachricht löschen { #delete-message }

`DELETE /Queue/deletemessage/{id}?messageId={messageId}` löscht eine einzelne Nachricht aus einer Queue.

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

```json title="Antwort"
{ "success": true }
```
