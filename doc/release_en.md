# Build & Release

MessageQueueNET is built with [Nuke](https://nuke.build). The same targets run locally and in GitHub Actions.

## Build locally

Requires the .NET 10 SDK (and Docker for the image targets).

```
./build.ps1                 # Windows (default target: Test)
./build.sh                  # Linux/macOS
./build.ps1 Clean Test Pack Zip
./build.ps1 PrintVersion
./build.ps1 --help          # list all targets and parameters
```

| Target | Description | Output |
|---|---|---|
| `Clean` | Deletes `artifacts/` | |
| `Compile` | Builds `MessageQueueNET.slnx` (Release) | |
| `Test` | Runs the tests | `artifacts/test-results/*.trx` |
| `Pack` | Creates the NuGet packages | `artifacts/packages/*.nupkg, *.snupkg` |
| `Zip` | Publishes API, dashboard and command line tools for `win-x64` and `linux-x64` | `artifacts/zip/messagequeue_net_<version>_<rid>.zip` |
| `DockerBuild` | Builds the Docker images (`linux-musl-x64`) | local images `ghcr.io/jugstalt/...` |
| `ReleaseNotes` | Extracts the section of the current version from `CHANGELOG.md` | `artifacts/release-notes.md` |
| `NuGetPush` | Pushes the packages (`--nuget-api-key` required) | |
| `DockerPush` | Pushes the images | |

The push targets are meant for the release workflow.

## Version

All packages, ZIPs and images share one version, calculated by [MinVer](https://github.com/adamralph/minver) from the git tag `v*`:

- On a tag `v4.1.0` the version is `4.1.0`.
- Between tags it is a pre-release like `4.1.1-alpha.0.3`.

The version numbers in the project files are not used anymore.

## Release

1. Add the changes to `CHANGELOG.md` under `## [X.Y.Z]`.
2. Create and push a tag:

   ```
   git tag v4.1.0
   git push origin v4.1.0
   ```

The workflow [release.yml](../.github/workflows/release.yml) then:

- builds and tests,
- pushes the NuGet packages to nuget.org (`MessageQueueNET.Core`, `.Client`, `.Worker`, `.Aspire.Hosting`, `.Cmd`),
- pushes the Docker images to ghcr.io:
  - `ghcr.io/jugstalt/messagequeue-net`
  - `ghcr.io/jugstalt/messagequeue-net-dashboard`
  - tags `X.Y.Z`, `X.Y` and `latest`,
- creates a GitHub release with the ZIPs and the NuGet packages as assets and the CHANGELOG section as release notes.

Tags with a pre-release suffix (e.g. `v4.0.0-preview.1`) create a pre-release on GitHub and NuGet. Their images only get the version tag (no `X.Y`, no `latest`).

## One-time setup

### NuGet trusted publishing

The release workflow does not use a stored NuGet API key. It gets a short-lived key via OIDC ([trusted publishing](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing)).

1. On nuget.org: *Account → Trusted Publishing → Create policy*
   - Repository owner: `jugstalt`
   - Repository: `MessageQueueNET`
   - Workflow file: `release.yml`
   - Environment: `release`
   - The policy owner must own the packages (`MessageQueueNET.*`).
2. In the GitHub repository: create the secret `NUGET_USER` with the nuget.org user name (not the e-mail address).
   The workflow uses the environment `release`; GitHub creates it on the first run, it can be protected with required reviewers.

### GitHub Container Registry

After the first release, check the packages `messagequeue-net` and `messagequeue-net-dashboard` under *Profile → Packages*:

- set the visibility to *Public*,
- link them to the repository `jugstalt/MessageQueueNET`.

## CI

[ci.yml](../.github/workflows/ci.yml) runs `./build.sh Test Pack` for pushes to `master` and `v4` and for pull requests. Test results and packages are uploaded as workflow artifacts.
