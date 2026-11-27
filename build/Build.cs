using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Serilog;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

class Build : NukeBuild
{
    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode
    public static int Main() => Execute<Build>(x => x.Test);

    [Parameter("Configuration to build - Default is 'Release'")]
    readonly string Configuration = "Release";

    [Parameter("Container registry including owner - Default is 'ghcr.io/jugstalt'")]
    readonly string ContainerRegistry = "ghcr.io/jugstalt";

    [Parameter("NuGet feed to push packages to - Default is nuget.org")]
    readonly string NuGetSource = "https://api.nuget.org/v3/index.json";

    [Parameter("NuGet API key (in CI a short-lived key from NuGet trusted publishing)"), Secret]
    readonly string NuGetApiKey;

    [Parameter("Container CLI with docker compatible build/push commands (e.g. 'wslc', 'podman') - Default is 'docker'")]
    readonly string ContainerCli = "docker";

    [Parameter("Checkout of docs-gviewonline for DocsPublish - Default is the folder docs-gviewonline next to this repository")]
    readonly string DocsRepository;

    const string RepositoryUrl = "https://github.com/jugstalt/MessageQueueNET";

    const string DocsFolder = "messagequeue-net";

    Tool _containerTool;
    Tool Docker => _containerTool ??= ToolResolver.GetPathTool(ContainerCli);

    static readonly string[] ZipRuntimes = ["win-x64", "linux-x64"];
    const string DockerRuntime = "linux-musl-x64";

    AbsolutePath SolutionFile => RootDirectory / "MessageQueueNET.slnx";
    AbsolutePath SourceDirectory => RootDirectory / "src";
    AbsolutePath DeployDirectory => RootDirectory / "deploy";
    AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";
    AbsolutePath PackagesDirectory => ArtifactsDirectory / "packages";
    AbsolutePath ZipDirectory => ArtifactsDirectory / "zip";
    AbsolutePath DockerDirectory => ArtifactsDirectory / "docker";
    AbsolutePath TestResultsDirectory => ArtifactsDirectory / "test-results";
    AbsolutePath ReleaseNotesFile => ArtifactsDirectory / "release-notes.md";

    AbsolutePath ApiProject => SourceDirectory / "backend" / "MessageQueueNET" / "MessageQueueNET.csproj";
    AbsolutePath DashboardProject => SourceDirectory / "frontend" / "MessageQueueNET.Blazor" / "MessageQueueNET.Blazor.csproj";
    AbsolutePath CmdProject => SourceDirectory / "console" / "MessageQueueNET.Cmd" / "MessageQueueNET.Cmd.csproj";
    AbsolutePath ProcessorProject => SourceDirectory / "console" / "MessageQueueNET.Processor" / "MessageQueueNET.Processor.csproj";
    AbsolutePath CoreProject => SourceDirectory / "shared" / "MessageQueueNET.Core" / "MessageQueueNET.Core.csproj";
    AbsolutePath TestProject => RootDirectory / "test" / "MessageQueueNET.Tests" / "MessageQueueNET.Tests.csproj";

    (string Name, AbsolutePath Project, string Image)[] DockerImages =>
    [
        ("api", ApiProject, "messagequeue-net"),
        ("dashboard", DashboardProject, "messagequeue-net-dashboard")
    ];

    string _version;

    // single source of truth: MinVer (git tag v*), the same value the projects are built with
    string Version => _version ??= DotNet(
            $"msbuild {CoreProject} -t:MinVer -getProperty:MinVerVersion -p:Configuration={Configuration} -nologo",
            logOutput: false)
        .StdToText()
        .Trim();

    bool IsPreRelease => Version.Contains('-');

    IEnumerable<string> ImageTags(string image)
    {
        var repository = $"{ContainerRegistry.TrimEnd('/')}/{image}";

        yield return $"{repository}:{Version}";

        if (!IsPreRelease)
        {
            var majorMinor = string.Join('.', Version.Split('.').Take(2));
            yield return $"{repository}:{majorMinor}";
            yield return $"{repository}:latest";
        }
    }

    Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
            ArtifactsDirectory.CreateOrCleanDirectory();
        });

    Target Restore => _ => _
        .Executes(() =>
        {
            DotNetRestore(s => s
                .SetProjectFile(SolutionFile));
        });

    Target PrintVersion => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            Log.Information("Version: {Version}", Version);
        });

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetBuild(s => s
                .SetProjectFile(SolutionFile)
                .SetConfiguration(Configuration)
                .EnableNoRestore());
        });

    Target Test => _ => _
        .DependsOn(Compile)
        .Produces(TestResultsDirectory / "*.trx")
        .Executes(() =>
        {
            DotNetTest(s => s
                .SetProjectFile(TestProject)
                .SetConfiguration(Configuration)
                .EnableNoRestore()
                .EnableNoBuild()
                .SetResultsDirectory(TestResultsDirectory)
                .AddLoggers("trx"));
        });

    Target Pack => _ => _
        .DependsOn(Compile)
        .After(Test)
        .Produces(PackagesDirectory / "*.nupkg", PackagesDirectory / "*.snupkg")
        .Executes(() =>
        {
            PackagesDirectory.CreateOrCleanDirectory();

            DotNetPack(s => s
                .SetProject(SolutionFile)
                .SetConfiguration(Configuration)
                .EnableNoRestore()
                .EnableNoBuild()
                .SetOutputDirectory(PackagesDirectory));
        });

    Target Zip => _ => _
        .DependsOn(Restore)
        .After(Test)
        .Produces(ZipDirectory / "*.zip")
        .Executes(() =>
        {
            ZipDirectory.CreateOrCleanDirectory();

            foreach (var runtime in ZipRuntimes)
            {
                var stagingDirectory = ArtifactsDirectory / "zip-staging" / runtime;
                var root = stagingDirectory / Version;
                stagingDirectory.CreateOrCleanDirectory();

                PublishWebApp(ApiProject, runtime, root / "api" / "app");
                PublishWebApp(DashboardProject, runtime, root / "dashboard" / "app");
                PublishConsoleApp(CmdProject, runtime, root / "cmd");
                PublishConsoleApp(ProcessorProject, runtime, root / "cmd");
                (root / "cmd").GlobFiles("*.pdb").DeleteFiles();

                var startScripts = runtime.StartsWith("win") ? "*.bat" : "*.sh";
                foreach (var script in (DeployDirectory / "zip").GlobFiles(startScripts))
                {
                    script.CopyToDirectory(root);
                }

                stagingDirectory.ZipTo(ZipDirectory / $"messagequeue_net_{Version}_{runtime}.zip");
                stagingDirectory.DeleteDirectory();
            }

            (ArtifactsDirectory / "zip-staging").DeleteDirectory();
        });

    Target DockerBuild => _ => _
        .DependsOn(Restore)
        .After(Test)
        .Executes(() =>
        {
            foreach (var (name, project, image) in DockerImages)
            {
                var context = DockerDirectory / name;
                context.CreateOrCleanDirectory();

                CopyDirectory(DeployDirectory / "docker" / name, context);
                PublishWebApp(project, DockerRuntime, context / "app");

                var tags = ImageTags(image).ToList();
                // local pre-release builds also get 'latest' so docker-compose.yml runs them; never pushed
                var latest = $"{ContainerRegistry.TrimEnd('/')}/{image}:latest";
                if (IsLocalBuild && !tags.Contains(latest))
                {
                    tags.Add(latest);
                }

                var tagArgs = string.Join(' ', tags.Select(t => $"--tag {t}"));
                // image.source links the package on ghcr.io to the repository (shown in the repository's sidebar)
                Docker($"build --pull --label org.opencontainers.image.source={RepositoryUrl} --label org.opencontainers.image.version={Version} {tagArgs:nq} {context}", logger: LogContainerOutput);
                Log.Information("Built {Tags}", tags);
            }
        });

    Target DockerPush => _ => _
        .DependsOn(DockerBuild)
        .Executes(() =>
        {
            foreach (var tag in DockerImages.SelectMany(i => ImageTags(i.Image)))
            {
                Docker($"push {tag}", logger: LogContainerOutput);
            }
        });

    Target NuGetPush => _ => _
        .DependsOn(Pack)
        .After(Test)
        .Requires(() => NuGetApiKey)
        .Executes(() =>
        {
            foreach (var package in PackagesDirectory.GlobFiles("*.nupkg"))
            {
                // the matching .snupkg next to the .nupkg is pushed automatically
                DotNetNuGetPush(s => s
                    .SetTargetPath(package)
                    .SetSource(NuGetSource)
                    .SetApiKey(NuGetApiKey)
                    .EnableSkipDuplicate());
            }
        });

    Target ReleaseNotes => _ => _
        .DependsOn(Restore)
        .Produces(ReleaseNotesFile)
        .Executes(() =>
        {
            var releaseVersion = Version.Split('-')[0];
            var changelog = (RootDirectory / "CHANGELOG.md").ReadAllText();
            var match = Regex.Match(
                changelog,
                $@"^## \[{Regex.Escape(releaseVersion)}\][^\n]*\n(?<notes>.*?)(?=^## |\z)",
                RegexOptions.Multiline | RegexOptions.Singleline);

            var notes = match.Success
                ? match.Groups["notes"].Value.Trim()
                : "See [CHANGELOG.md](https://github.com/jugstalt/MessageQueueNET/blob/master/CHANGELOG.md).";

            ArtifactsDirectory.CreateDirectory();
            ReleaseNotesFile.WriteAllText(notes + Environment.NewLine);
        });

    // Static documentation site (MkDocs Material, see mkdocs.yml) in artifacts/docs/site.
    // Needs Python; without it the target is skipped with a warning.
    Target Docs => _ => _
        .Executes(() =>
        {
            // never publish the site of an earlier build when this one is skipped or fails
            (ArtifactsDirectory / "docs").DeleteDirectory();

            // MkDocs in its own virtual environment .venv-docs, created on the first run
            var venv = RootDirectory / ".venv-docs";
            var python = OperatingSystem.IsWindows() ? venv / "Scripts" / "python.exe" : venv / "bin" / "python";
            if (!python.FileExists())
            {
                var check = ProcessTasks.StartProcess("python", $"-m venv \"{venv}\"", workingDirectory: RootDirectory, logger: (_, _) => { }, timeout: 120000);
                check.WaitForExit();
                if (check.ExitCode != 0 || !python.FileExists())
                {
                    Log.Warning("Docs skipped: Python not found (needed for MkDocs)");
                    return;
                }
            }

            ProcessTasks.StartProcess(python, "-m pip install --quiet --disable-pip-version-check -r docs/requirements.txt",
                    workingDirectory: RootDirectory,
                    logger: (_, text) => Log.Debug(text))
                .AssertZeroExitCode();

            // strict: broken links fail the build
            ProcessTasks.StartProcess(python, "-m mkdocs build --strict --clean",
                    workingDirectory: RootDirectory,
                    logger: (_, text) => Log.Information(text))
                .AssertZeroExitCode();

            Log.Information("Docs site: {Site}", ArtifactsDirectory / "docs" / "site");
        });

    // Mirrors the site into app/other/messagequeue-net of the docs-gviewonline repository, whose app folder
    // a GitHub action deploys as it is. Nothing is committed or pushed here; do that in docs-gviewonline.
    //   .\build.cmd DocsPublish
    //   .\build.cmd DocsPublish --docs-repository D:\github\docs-gviewonline
    Target DocsPublish => _ => _
        .DependsOn(Docs)
        .Executes(() =>
        {
            var site = ArtifactsDirectory / "docs" / "site";
            Assert.DirectoryExists(site, "The Docs target made no site (is Python installed?)");

            var repository = DocsRepository is null ? RootDirectory.Parent / "docs-gviewonline" : (AbsolutePath)DocsRepository;
            if (!(repository / "app").DirectoryExists())
            {
                Log.Warning("{Repository} is no checkout of docs-gviewonline (no app folder); set --docs-repository. Docs not copied.", repository);
                return;
            }

            var target = repository / "app" / "other" / DocsFolder;

            // mirror, not merge: renamed or removed pages must not stay behind
            target.DeleteDirectory();
            site.Copy(target, ExistsPolicy.MergeAndOverwrite);

            Log.Information("Docs copied to {Target}. Commit and push it in {Repository} to publish.", target, repository);
        });

    void PublishWebApp(AbsolutePath project, string runtime, AbsolutePath output)
    {
        DotNetPublish(s => s
            .SetProject(project)
            .SetConfiguration(Configuration)
            .SetRuntime(runtime)
            .DisableSelfContained()
            .SetPublishReadyToRun(true)
            .SetOutput(output));

        // never ship the developer configuration (subfolders like _config/proto are kept)
        (output / "_config").GlobFiles("*").DeleteFiles();
    }

    void PublishConsoleApp(AbsolutePath project, string runtime, AbsolutePath output)
    {
        DotNetPublish(s => s
            .SetProject(project)
            .SetConfiguration(Configuration)
            .SetRuntime(runtime)
            .DisableSelfContained()
            .SetPublishSingleFile(true)
            .SetPublishReadyToRun(true)
            .SetOutput(output));
    }

    // docker/wslc write build progress to stderr; real failures are reported via the exit code
    static void LogContainerOutput(OutputType type, string text) => Log.Information(text);

    static void CopyDirectory(AbsolutePath source, AbsolutePath target)
    {
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var destination = target / Path.GetRelativePath(source, file);
            destination.Parent.CreateDirectory();
            File.Copy(file, destination, overwrite: true);
        }
    }
}
