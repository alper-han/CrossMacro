using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CrossMacro.Daemon.Contracts.Ipc;
using CrossMacro.Daemon.Contracts.Security;

namespace CrossMacro.Platform.Linux.Tests.Packaging;

public sealed partial class LinuxPackagingStaticParityTests
{
    private const string CanonicalSocketPath = "/run/crossmacro/crossmacro.sock";
    private const string HostDaemonFilesystemArg = "--filesystem=/run/crossmacro:rw";
    private const string DeviceAllArg = "--device=all";
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void FlatpakWrapperAndDocs_ShouldReferenceCanonicalDaemonSocketPath()
    {
        Assert.Equal(CanonicalSocketPath, IpcProtocol.DefaultSocketPath);

        var referencedFiles = new[]
        {
            "flatpak/crossmacro.sh",
            "README.md",
            "docs/man/crossmacro.1",
            "docs/linux-daemon-packaging-audit.md"
        };

        foreach (var relativePath in referencedFiles)
        {
            var text = ReadRepoFile(relativePath);

            Assert.Contains(CanonicalSocketPath, text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void FlatpakManifests_ShouldKeepMatchingHostDaemonExposureAndImportantFinishArgs()
    {
        var manifestPaths = new[]
        {
            "flatpak/io.github.alper_han.crossmacro.yml",
            "flatpak/io.github.alper_han.crossmacro.flathub.yml"
        };

        var expectedFinishArgs = new[]
        {
            "--socket=x11",
            "--share=ipc",
            DeviceAllArg,
            "--talk-name=org.kde.keyboard",
            "--talk-name=org.kde.KWin",
            "--talk-name=org.gnome.Shell",
            "--talk-name=org.freedesktop.Flatpak",
            "--filesystem=xdg-run/hypr:ro",
            HostDaemonFilesystemArg,
            "--filesystem=~/.local/share/gnome-shell/extensions:create",
            "--env=DOTNET_ROOT=/app/lib/dotnet",
            "--env=CROSSMACRO_FLATPAK=1"
        };

        var firstManifestArgs = ReadFinishArgs(manifestPaths[0]);

        Assert.Equal(expectedFinishArgs, firstManifestArgs);

        foreach (var manifestPath in manifestPaths)
        {
            var finishArgs = ReadFinishArgs(manifestPath);

            Assert.Equal(firstManifestArgs, finishArgs);
            Assert.Contains(HostDaemonFilesystemArg, finishArgs);
            Assert.Contains(DeviceAllArg, finishArgs);
        }
    }

    [Fact]
    public void DaemonService_ShouldKeepSystemdRuntimeDirectoryContract()
    {
        var service = ReadRepoFile("scripts/daemon/crossmacro.service");

        Assert.Contains("RuntimeDirectory=crossmacro", service, StringComparison.Ordinal);
        Assert.Contains("RuntimeDirectoryMode=0750", service, StringComparison.Ordinal);
        Assert.Contains("RuntimeDirectoryPreserve=yes", service, StringComparison.Ordinal);
    }

    [Fact]
    public void PolkitContractsAndAssets_ShouldKeepMatchingActionIds()
    {
        var policyActions = ExtractPolkitActionIds(ReadRepoFile("scripts/assets/io.github.alper_han.crossmacro.policy"));
        var rulesActions = ExtractPolkitActionIds(ReadRepoFile("scripts/assets/50-crossmacro.rules"));

        var expectedActions = PolkitActions.All;

        Assert.Equal(expectedActions, policyActions);
        Assert.Equal(expectedActions, rulesActions);

        Assert.Contains(
            "<annotate key=\"org.freedesktop.policykit.imply\">io.github.alper_han.crossmacro.input-simulate</annotate>",
            ReadRepoFile("scripts/assets/io.github.alper_han.crossmacro.policy"),
            StringComparison.Ordinal);
    }

    [Fact]
    public void PackageSources_ShouldReferenceDaemonServicePolkitUdevAndModulesAssets()
    {
        var requiredReferencesBySource = new Dictionary<string, string[]>
        {
            ["scripts/build_deb.sh"] =
            [
                "daemon/crossmacro.service",
                "assets/io.github.alper_han.crossmacro.policy",
                "assets/50-crossmacro.rules",
                "assets/99-crossmacro.rules",
                "assets/crossmacro-modules.conf"
            ],
            ["scripts/build_rpm.sh"] =
            [
                "daemon/crossmacro.service",
                "assets/io.github.alper_han.crossmacro.policy",
                "assets/50-crossmacro.rules",
                "assets/99-crossmacro.rules",
                "assets/crossmacro-modules.conf"
            ],
            ["scripts/packaging/arch/PKGBUILD"] =
            [
                "scripts/daemon/crossmacro.service",
                "scripts/assets/io.github.alper_han.crossmacro.policy",
                "scripts/assets/50-crossmacro.rules",
                "scripts/assets/99-crossmacro.rules",
                "crossmacro-modules.conf"
            ],
            ["scripts/packaging/rpm/crossmacro.spec"] =
            [
                "crossmacro.service",
                "io.github.alper_han.crossmacro.policy",
                "50-crossmacro.rules",
                "99-crossmacro.rules",
                "crossmacro-modules.conf"
            ],
            ["scripts/daemon/install.sh"] =
            [
                "scripts/assets/99-crossmacro.rules",
                "scripts/assets/crossmacro-modules.conf",
                "scripts/assets/io.github.alper_han.crossmacro.policy",
                "scripts/assets/50-crossmacro.rules",
                "crossmacro.service"
            ]
        };

        foreach (var (sourcePath, references) in requiredReferencesBySource)
        {
            var text = ReadRepoFile(sourcePath);

            foreach (var reference in references)
            {
                Assert.Contains(reference, text, StringComparison.Ordinal);
            }
        }
    }

    private static string[] ReadFinishArgs(string relativePath)
    {
        var lines = ReadRepoFile(relativePath).Split('\n');
        var args = new List<string>();
        var inFinishArgs = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');

            if (line == "finish-args:")
            {
                inFinishArgs = true;
                continue;
            }

            if (inFinishArgs && line.Length > 0 && !char.IsWhiteSpace(line[0]))
            {
                break;
            }

            if (!inFinishArgs)
            {
                continue;
            }

            var trimmed = line.Trim();
            if (trimmed.StartsWith("- ", StringComparison.Ordinal))
            {
                args.Add(trimmed[2..]);
            }
        }

        return args.ToArray();
    }

    private static string[] ExtractPolkitActionIds(string text)
    {
        return PolkitActionIdRegex()
            .Matches(text)
            .Select(match => match.Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string ReadRepoFile(string relativePath)
    {
        return File.ReadAllText(Path.Combine(RepoRoot, relativePath));
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CrossMacro.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from test output directory.");
    }

    [GeneratedRegex("io\\.github\\.alper_han\\.crossmacro\\.input-(?:capture|simulate)")]
    private static partial Regex PolkitActionIdRegex();
}
