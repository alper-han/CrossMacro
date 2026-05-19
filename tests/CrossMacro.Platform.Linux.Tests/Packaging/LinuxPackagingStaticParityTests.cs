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
            "docs/man/crossmacro.1"
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
            ["scripts/packaging/deb/build.sh"] =
            [
                "daemon/crossmacro.service",
                "assets/io.github.alper_han.crossmacro.policy",
                "assets/50-crossmacro.rules",
                "assets/99-crossmacro.rules",
                "assets/crossmacro-modules.conf"
            ],
            ["scripts/packaging/rpm/build.sh"] =
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

    [Fact]
    public void LinuxPackages_ShouldDeclareIcuWhenUiUsesFullGlobalization()
    {
        var rpmSpec = ReadRepoFile("scripts/packaging/rpm/crossmacro.spec");
        var debScript = ReadRepoFile("scripts/packaging/deb/build.sh");
        var archPkgbuild = ReadRepoFile("scripts/packaging/arch/PKGBUILD");
        var appImageScript = ReadRepoFile("scripts/packaging/appimage/build.sh");
        var linuxUiProject = ReadRepoFile("src/CrossMacro.UI.Linux/CrossMacro.UI.Linux.csproj");
        var sharedUiProject = ReadRepoFile("src/CrossMacro.UI/CrossMacro.UI.csproj");

        Assert.Contains("<InvariantGlobalization>false</InvariantGlobalization>", linuxUiProject, StringComparison.Ordinal);
        Assert.Contains("<InvariantGlobalization>false</InvariantGlobalization>", sharedUiProject, StringComparison.Ordinal);

        Assert.Contains("libicu", ExtractRpmRequires(rpmSpec));
        Assert.Contains("libicu74", ExtractDebControlFieldValues(debScript, "Depends"));
        Assert.Contains("icu", ExtractArchDepends(archPkgbuild));
        Assert.Contains("resolve_latest_icu_version", appImageScript, StringComparison.Ordinal);
        Assert.Contains("copy_icu_library_family", appImageScript, StringComparison.Ordinal);
        Assert.Contains("libicudata.so.$version", appImageScript, StringComparison.Ordinal);
        Assert.Contains("libicui18n.so.$version", appImageScript, StringComparison.Ordinal);
        Assert.Contains("libicuuc.so.$version", appImageScript, StringComparison.Ordinal);
        Assert.Contains("DOTNET_SYSTEM_GLOBALIZATION_APPLOCALICU=\"$ICU_VERSION\"", appImageScript, StringComparison.Ordinal);
        Assert.Contains("LD_LIBRARY_PATH=\"\\$HERE/usr/lib", appImageScript, StringComparison.Ordinal);
    }

    [Fact]
    public void ArchInstallHook_ShouldReportUserGroupChangesTruthfully()
    {
        var installHook = ReadRepoFile("scripts/packaging/arch/crossmacro.install");

        Assert.DoesNotContain("usermod -aG crossmacro \"$installer_user\" >/dev/null 2>&1 || true", installHook, StringComparison.Ordinal);
        Assert.DoesNotContain("was added to 'crossmacro' group (best effort)", installHook, StringComparison.Ordinal);
        Assert.Contains("elif usermod -aG crossmacro \"$installer_user\" >/dev/null 2>&1; then", installHook, StringComparison.Ordinal);
        Assert.Contains("installer_user_group_status=\"already_member\"", installHook, StringComparison.Ordinal);
        Assert.Contains("installer_user_group_status=\"added\"", installHook, StringComparison.Ordinal);
        Assert.Contains("installer_user_group_status=\"failed\"", installHook, StringComparison.Ordinal);
        Assert.Contains("installer_user_group_status=\"unknown\"", installHook, StringComparison.Ordinal);
        Assert.Contains("'$installer_user' is already a member of the 'crossmacro' group.", installHook, StringComparison.Ordinal);
        Assert.Contains("'$installer_user' was added to the 'crossmacro' group.", installHook, StringComparison.Ordinal);
        Assert.Contains("Could not add '$installer_user' to the 'crossmacro' group automatically.", installHook, StringComparison.Ordinal);
        Assert.Contains("Could not determine the non-root user who launched the installer.", installHook, StringComparison.Ordinal);
        Assert.Contains("sudo usermod -aG crossmacro \\$USER", installHook, StringComparison.Ordinal);
        Assert.Contains("log out and log back in, or reboot", installHook, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ArchInstallHook_ShouldProvisionSysusersBeforeServiceStartAndRestart()
    {
        var installHook = ReadRepoFile("scripts/packaging/arch/crossmacro.install");
        var postInstallSection = ExtractSection(installHook, "post_install() {", "post_upgrade() {");
        var postUpgradeSection = ExtractSection(installHook, "post_upgrade() {", "pre_remove() {");

        Assert.Contains("_crossmacro_provision_sysusers()", installHook, StringComparison.Ordinal);
        Assert.Contains("getent passwd crossmacro >/dev/null 2>&1 || return 1", installHook, StringComparison.Ordinal);
        Assert.Contains("getent group crossmacro >/dev/null 2>&1 || return 1", installHook, StringComparison.Ordinal);

        AssertOrder(
            postInstallSection,
            "if _crossmacro_provision_sysusers; then",
            "systemctl enable --now crossmacro.service");
        AssertOrder(
            postUpgradeSection,
            "if ! _crossmacro_provision_sysusers; then",
            "systemctl try-restart crossmacro.service");
    }

    private static void AssertOrder(string text, string first, string last)
    {
        var firstIndex = text.IndexOf(first, StringComparison.Ordinal);
        var lastIndex = text.IndexOf(last, StringComparison.Ordinal);

        Assert.True(firstIndex >= 0, $"Could not find '{first}' in packaging hook.");
        Assert.True(lastIndex >= 0, $"Could not find '{last}' in packaging hook.");
        Assert.True(firstIndex < lastIndex, $"Expected '{first}' before '{last}'.");
    }

    private static string ExtractSection(string text, string startMarker, string endMarker)
    {
        var startIndex = text.IndexOf(startMarker, StringComparison.Ordinal);
        var endIndex = text.IndexOf(endMarker, startIndex + startMarker.Length, StringComparison.Ordinal);

        Assert.True(startIndex >= 0, $"Could not find '{startMarker}' in packaging hook.");
        Assert.True(endIndex >= 0, $"Could not find '{endMarker}' in packaging hook.");

        return text.Substring(startIndex, endIndex - startIndex);
    }

    private static string[] ExtractRpmRequires(string spec)
    {
        var requiresLine = spec
            .Split('\n')
            .Select(line => line.Trim())
            .Single(line => line.StartsWith("Requires:", StringComparison.Ordinal));

        return requiresLine["Requires:".Length..]
            .Split(',')
            .Select(dependency => dependency.Trim())
            .Where(dependency => dependency.Length > 0)
            .ToArray();
    }

    private static string[] ExtractDebControlFieldValues(string script, string fieldName)
    {
        var fieldLine = script
            .Split('\n')
            .Select(line => line.Trim())
            .Single(line => line.StartsWith($"{fieldName}:", StringComparison.Ordinal));

        return fieldLine[(fieldName.Length + 1)..]
            .Split(',')
            .SelectMany(group => group.Split('|'))
            .Select(dependency => dependency.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)[0])
            .Where(dependency => dependency.Length > 0)
            .ToArray();
    }

    private static string[] ExtractArchDepends(string pkgbuild)
    {
        var dependsLine = pkgbuild
            .Split('\n')
            .Select(line => line.Trim())
            .Single(line => line.StartsWith("depends=", StringComparison.Ordinal));

        return Regex.Matches(dependsLine, "'([^']+)'")
            .Select(match => match.Groups[1].Value)
            .ToArray();
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
