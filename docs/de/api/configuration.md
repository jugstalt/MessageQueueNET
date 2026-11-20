# Konfiguration

Die REST-API liest ihre Einstellungen aus `appsettings.json`, aus `_config/message-queue.json` und aus
Umgebungsvariablen (`MESSAGEQUEUE__PERSIST__TYPE`, …).

!!! tip "`_config/message-queue.json` verwenden"
    Eigene Einstellungen gehören in `_config/message-queue.json`. Diese Datei wird bei einem Update nie
    überschrieben: Für ein Update alles außer dem Ordner `_config` über die bestehende Installation kopieren.

## Beispiel

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

## Persistierung

Standardmäßig liegen die Queues **im Speicher**. Das ist schnell, aber alle Queues sind weg, wenn die Anwendung
beendet oder recycelt wird.

Steht `MessageQueue:Persist:Type` auf `filesystem`, wird jede Änderung zusätzlich nach `RootPath` geschrieben.
Beim Start stellt die Anwendung den letzten Stand wieder her. Sehr lange Queues können den Start etwas verzögern.

| Einstellung | Beschreibung |
|---|---|
| `Persist:Type` | `filesystem` oder leer (nur im Speicher) |
| `Persist:RootPath` | Ordner für die persistierten Queues |

!!! note
    Persistierte Queues, deren Namen nicht mehr gültig sind (siehe [Queue-Namen](rest.md#queue-names)), werden
    beim Start übersprungen. Es wird eine Warnung geloggt, die Dateien werden nicht gelöscht.

## Limits

`MessageQueue:Limits` schützt den Dienst vor Ressourcenerschöpfung. Ein Wert `<= 0` bedeutet *unbegrenzt*.

| Einstellung | Standard | Beschreibung |
|---|---|---|
| `MaxQueues` | `10000` | Maximale Anzahl an Queues. Weitere Queues anzulegen schlägt fehl. |
| `MaxMessageBytes` | `1048576` | Maximale Größe einer einzelnen Nachricht (UTF-8-Bytes). |
| `MaxDequeueCount` | `1000` | Obergrenze für den Parameter `count` von `dequeue`. |
| `MaxQueueLength` | `0` | Maximale Anzahl an Nachrichten in einer Queue. |

## Long Polling

Clients können auf Änderungen warten, statt in einer Schleife abzufragen. Jede Antwort enthält einen `hashCode`
der angefragten Queues. Ein Client schickt ihn im Header `X-MQ-HashCode` zurück; der Server hält die Anfrage dann,
bis sich die Queues ändern oder die Polling-Zeit abgelaufen ist.

| Einstellung / Header | Standard | Beschreibung |
|---|---|---|
| `MessageQueue:MaxRequestPollingSeconds` | `60` | Maximale Zeit, die eine Anfrage gehalten wird |
| `X-MQ-MaxPollingSeconds` (Header) | | Kürzere Polling-Zeit für eine einzelne Anfrage (begrenzt durch die Einstellung oben) |

Der [.NET-Client](../client.md) und die [Patterns](../patterns/queue-worker.md) verwenden Long Polling automatisch.

## Autorisierung

Ohne Abschnitt `Authorization` ist die Queue **für alle offen** (beim Start wird eine Warnung geloggt).
Für den Produktivbetrieb `Basic`-Authentifizierung konfigurieren (derzeit der einzige unterstützte Typ) und die
Clients unter `Authorization:Clients` eintragen.

Clients schicken ihre Zugangsdaten als HTTP-Header (`Authorization: Basic base64(id:secret)`) oder im URL-Format
(`https://clientId:clientSecret@my-server`).

!!! warning "HTTPS verwenden"
    Basic Authentication überträgt die Zugangsdaten im Klartext. HTTPS verwenden, direkt oder über einen
    Reverse Proxy, der TLS terminiert.

Mit aktivierter Authentifizierung braucht jede Anfrage gültige Zugangsdaten, auch `/info`, das OpenAPI-Dokument
und die Scalar-UI. Nur die Health-Check-Endpunkte `/health` und `/alive` sind anonym.

### Secrets

Secrets können im Klartext oder als SHA-256-Hash gespeichert werden: `"Secret": "sha256:<64 Hex-Zeichen>"`.

=== "Linux"

    ```bash
    printf '%s' 'my-secret' | sha256sum
    ```

=== "PowerShell"

    ```powershell
    [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes('my-secret')))
    ```

### Rollen

Die Rollen sind hierarchisch: `admin` enthält `write`, `write` enthält `read`.
Ein Client ohne `Roles` bekommt die Rolle `admin` (Abwärtskompatibilität).

| Rolle | Erlaubte Methoden |
|---|---|
| `read` | `info`, `all`, `length`, `properties`, `queuenames` |
| `write` | `read` + `enqueue`, `dequeue`, `confirmdequeue`, `register`/`remove` für eine einzelne Queue (kein Muster) |
| `admin` | `write` + `register`/`remove` mit Mustern, `deletemessage` |

Fehlende oder ungültige Zugangsdaten liefern `401`, fehlende Rollen `403`.

Mit aktivierter Authentifizierung kann nur der Client eine Nachricht bestätigen, der sie abgeholt hat.

## OpenAPI

| Einstellung | Beschreibung |
|---|---|
| `openApiUI` (Umgebungsvariable `OPENAPIUI`) | `true` aktiviert `/openapi/v1.json` und die Scalar-UI unter `/scalar` auch außerhalb der Umgebung `Development`. Die alte Einstellung `swaggerUI` funktioniert weiterhin als Fallback. |

## Fehler

Bei Fehlern liefern die Methoden `success: false` und eine `errorMessage`. Nur erwartete Fehler (z. B. ungültiger
Queue-Name, Limit überschritten, Queue ausgesetzt) liefern eine aussagekräftige Meldung. Unerwartete Fehler
liefern `Internal server error`; die Details stehen im Log.
