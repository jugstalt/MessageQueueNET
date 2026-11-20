# Build & Release

MessageQueueNET wird mit [Nuke](https://nuke.build) gebaut. Lokal und in GitHub Actions laufen dieselben Targets.

## Lokal bauen

Benötigt das .NET 10 SDK (und Docker für die Image-Targets).

```powershell
./build.ps1                 # Windows (Standard-Target: Test)
./build.sh                  # Linux/macOS
./build.ps1 Clean Test Pack Zip
./build.ps1 PrintVersion
./build.ps1 --help          # alle Targets und Parameter auflisten
```

| Target | Beschreibung | Ausgabe |
|---|---|---|
| `Clean` | Löscht `artifacts/` | |
| `Compile` | Baut `MessageQueueNET.slnx` (Release) | |
| `Test` | Führt die Tests aus | `artifacts/test-results/*.trx` |
| `Pack` | Erstellt die NuGet-Pakete | `artifacts/packages/*.nupkg, *.snupkg` |
| `Zip` | Veröffentlicht API, Dashboard und Kommandozeilen-Tools für `win-x64` und `linux-x64` | `artifacts/zip/messagequeue_net_<version>_<rid>.zip` |
| `DockerBuild` | Baut die Docker-Images (`linux-musl-x64`); mit `--container-cli wslc` (oder `podman`) statt Docker | lokale Images `ghcr.io/jugstalt/...` |
| `ReleaseNotes` | Extrahiert den Abschnitt der aktuellen Version aus `CHANGELOG.md` | `artifacts/release-notes.md` |
| `NuGetPush` | Pusht die Pakete (`--nuget-api-key` erforderlich) | |
| `DockerPush` | Pusht die Images | |
| `Docs` | Baut diese Dokumentation mit MkDocs (benötigt Python 3) | `artifacts/docs` |
| `DocsPublish` | Kopiert die Dokumentation in einen Checkout von [docs-gviewonline](https://github.com/jugstalt/docs-gviewonline) | `app/other/messagequeue-net` |

Die Push-Targets sind für den Release-Workflow gedacht.

## Version

Alle Pakete, ZIPs und Images haben dieselbe Version. Sie wird von [MinVer](https://github.com/adamralph/minver)
aus dem Git-Tag `v*` berechnet:

- Auf einem Tag `v4.1.0` ist die Version `4.1.0`.
- Zwischen Tags ist es eine Vorabversion wie `4.1.1-alpha.0.3`.

Die Versionsnummern in den Projektdateien werden nicht mehr verwendet.

## Release

1. Die Änderungen in `CHANGELOG.md` unter `## [X.Y.Z]` eintragen.
2. Tag anlegen und pushen:

   ```
   git tag v4.1.0
   git push origin v4.1.0
   ```

Der Workflow [release.yml](https://github.com/jugstalt/MessageQueueNET/blob/master/.github/workflows/release.yml) dann:

- baut und testet,
- pusht die NuGet-Pakete nach nuget.org (`MessageQueueNET.Core`, `.Client`, `.Worker`, `.Aspire.Hosting`, `.Cmd`),
- pusht die Docker-Images nach ghcr.io:
  - `ghcr.io/jugstalt/messagequeue-net`
  - `ghcr.io/jugstalt/messagequeue-net-dashboard`
  - Tags `X.Y.Z`, `X.Y` und `latest`,
- erstellt ein GitHub-Release mit den ZIPs und NuGet-Paketen als Assets und dem CHANGELOG-Abschnitt als Release Notes.

Tags mit Vorabversions-Suffix (z. B. `v4.0.0-preview.1`) erzeugen eine Vorabversion auf GitHub und NuGet. Ihre
Images bekommen nur den Versions-Tag (kein `X.Y`, kein `latest`).

## Einmalige Einrichtung

### NuGet Trusted Publishing

Der Release-Workflow verwendet keinen gespeicherten NuGet-API-Key. Er holt sich per OIDC einen kurzlebigen Key
([Trusted Publishing](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing)).

1. Auf nuget.org: *Account → Trusted Publishing → Create policy*
   - Repository owner: `jugstalt`
   - Repository: `MessageQueueNET`
   - Workflow file: `release.yml`
   - Environment: `release`
   - Der Besitzer der Policy muss die Pakete (`MessageQueueNET.*`) besitzen.
2. Im GitHub-Repository: das Secret `NUGET_USER` mit dem nuget.org-Benutzernamen anlegen (nicht die E-Mail-Adresse).
   Der Workflow verwendet die Umgebung `release`; GitHub legt sie beim ersten Lauf an, sie kann mit Pflicht-Reviewern
   geschützt werden.

### GitHub Container Registry

Nach dem ersten Release die Pakete `messagequeue-net` und `messagequeue-net-dashboard` unter *Profile → Packages* prüfen:

- Sichtbarkeit auf *Public* setzen,
- mit dem Repository `jugstalt/MessageQueueNET` verknüpfen.

## CI

[ci.yml](https://github.com/jugstalt/MessageQueueNET/blob/master/.github/workflows/ci.yml) führt `./build.sh Test Pack`
für Pushes auf `master` und `v4` sowie für Pull Requests aus. Testergebnisse und Pakete werden als
Workflow-Artefakte hochgeladen.
