
namespace CrossMacro.Platform.Linux.Tests.Packaging;

public sealed partial class LinuxPackagingStaticParityTests
{
    private const string CanonicalSocketPath = "/run/crossmacro/crossmacro.sock";
    private const string NativeDesktopId = "CrossMacro.desktop";
    private const string FlatpakDesktopId = "io.github.alper_han.crossmacro.desktop";
    private const string KWinScreenShotPermission = "org.kde.KWin.ScreenShot2";
    private const string HostDaemonFilesystemArg = "--filesystem=/run/crossmacro:rw";
    private const string DeviceAllArg = "--device=all";
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void PortablePackageLaunchers_ShouldNotReferenceDaemonSocket()
    {
        Assert.Equal(CanonicalSocketPath, IpcProtocol.DefaultSocketPath);

        var referencedFiles = new[]
        {
            "flatpak/io.github.alper_han.crossmacro.yml",
            "flatpak/io.github.alper_han.crossmacro.flathub.yml",
            "scripts/packaging/appimage/build.sh",
        };

        foreach (var relativePath in referencedFiles)
        {
            var text = ReadRepoFile(relativePath);

            Assert.DoesNotContain(CanonicalSocketPath, text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void FlatpakManifests_ShouldKeepMatchingDirectDevicePermissions()
    {
        var manifestPaths = new[]
        {
            "flatpak/io.github.alper_han.crossmacro.yml",
            "flatpak/io.github.alper_han.crossmacro.flathub.yml",
        };

        var expectedFinishArgs = new[]
        {
            "--socket=wayland",
            "--socket=fallback-x11",
            "--share=ipc",
            DeviceAllArg,
            "--talk-name=org.kde.keyboard",
            "--talk-name=org.kde.KWin",
            "--talk-name=org.gnome.Shell",
            "--talk-name=org.freedesktop.Flatpak",
            "--filesystem=xdg-run/hypr:ro",
            "--filesystem=~/.local/share/gnome-shell/extensions:create",
            "--env=CROSSMACRO_FLATPAK=1",
        };

        var firstManifestArgs = ReadFinishArgs(manifestPaths[0]);

        Assert.Equal(expectedFinishArgs, firstManifestArgs);

        foreach (var manifestPath in manifestPaths)
        {
            var finishArgs = ReadFinishArgs(manifestPath);

            Assert.Equal(firstManifestArgs, finishArgs);
            Assert.DoesNotContain(HostDaemonFilesystemArg, finishArgs);
            Assert.Contains(DeviceAllArg, finishArgs);
        }
    }

    [Fact]
    public void NativeDesktopAsset_ShouldUseDistinctIdAndDeclareKWinPermission()
    {
        var desktop = ReadDesktopEntry($"scripts/assets/{NativeDesktopId}");

        Assert.Equal("crossmacro", desktop["Exec"]);
        Assert.Equal("CrossMacro.UI", desktop["StartupWMClass"]);
        Assert.Equal(KWinScreenShotPermission, desktop["X-KDE-DBUS-Restricted-Interfaces"]);
        Assert.NotEqual(NativeDesktopId, FlatpakDesktopId);
    }

    [Fact]
    public void NativePackageDefinitions_ShouldLaunchTheirInstalledGuiElfDirectly()
    {
        var packageSources = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["scripts/packaging/deb/build.sh"] = "Exec=/usr/lib/crossmacro/CrossMacro.UI",
            ["scripts/packaging/rpm/crossmacro.spec"] = "Exec=\\/usr\\/lib\\/crossmacro\\/CrossMacro.UI",
            ["scripts/packaging/arch/PKGBUILD"] = "Exec=/usr/lib/crossmacro/CrossMacro.UI",
            ["scripts/packaging/arch/PKGBUILD-git.in"] = "Exec=/usr/lib/crossmacro/CrossMacro.UI",
        };

        foreach (var (packageSource, desktopExec) in packageSources)
        {
            var text = ReadRepoFile(packageSource);

            Assert.Contains(NativeDesktopId, text, StringComparison.Ordinal);
            Assert.Contains(desktopExec, text, StringComparison.Ordinal);
            Assert.DoesNotContain(FlatpakDesktopId, text, StringComparison.Ordinal);
        }

        Assert.Contains(NativeDesktopId, ReadRepoFile("scripts/packaging/rpm/build.sh"), StringComparison.Ordinal);
        Assert.Contains("linux-desktop-identity.sh", ReadRepoFile("scripts/smoke/deb-package.sh"), StringComparison.Ordinal);
        Assert.Contains("linux-desktop-identity.sh", ReadRepoFile("scripts/smoke/rpm-package.sh"), StringComparison.Ordinal);
        Assert.Contains("file -L \"$executable\"", ReadRepoFile("scripts/smoke/linux-desktop-identity.sh"), StringComparison.Ordinal);

        var releaseWorkflow = ReadRepoFile(".github/workflows/release.yml");
        Assert.Contains($"usr/share/applications/{NativeDesktopId}", releaseWorkflow, StringComparison.Ordinal);
        Assert.Contains("crossmacro_validate_native_desktop_identity /", releaseWorkflow, StringComparison.Ordinal);
    }

    [Fact]
    public void SandboxAndAppImagePackages_ShouldKeepTheirDeliberateCaptureStrategies()
    {
        var flatpakDesktop = ReadDesktopEntry($"flatpak/{FlatpakDesktopId}");
        var flatpakManifest = ReadRepoFile("flatpak/io.github.alper_han.crossmacro.yml");
        var backendPolicy = ReadRepoFile("src/CrossMacro.Platform.Linux/Services/ScreenReading/LinuxScreenReaderBackendPolicy.cs");
        var appImageBuild = ReadRepoFile("scripts/packaging/appimage/build.sh");
        var kWinCapture = ReadRepoFile("src/CrossMacro.Platform.Linux/DisplayServer/Wayland/KWinScreenShotCapture.cs");

        Assert.Equal("crossmacro", flatpakDesktop["Exec"]);
        Assert.Equal("io.github.alper_han.crossmacro", flatpakDesktop["X-Flatpak"]);
        Assert.DoesNotContain("X-KDE-DBUS-Restricted-Interfaces", flatpakDesktop.Keys);
        Assert.Contains("ln -s ../lib/crossmacro/CrossMacro.UI /app/bin/crossmacro", flatpakManifest, StringComparison.Ordinal);
        Assert.DoesNotContain("crossmacro.sh", flatpakManifest, StringComparison.Ordinal);

        var flatpakPolicy = ExtractSection(backendPolicy, "FlatpakWaylandOrder =", "];", includeEndMarker: true);
        Assert.Contains("LinuxScreenReaderBackend.Portal", flatpakPolicy, StringComparison.Ordinal);
        Assert.DoesNotContain("LinuxScreenReaderBackend.KWinScreenShot2", flatpakPolicy, StringComparison.Ordinal);

        Assert.Contains("Exec=AppRun", appImageBuild, StringComparison.Ordinal);
        Assert.Contains("exec \"\\$HERE/usr/bin/CrossMacro.UI\"", appImageBuild, StringComparison.Ordinal);
        Assert.Contains("File.ResolveLinkTarget(\"/proc/self/exe\"", kWinCapture, StringComparison.Ordinal);
        Assert.Contains("Exec={canonicalExe}", kWinCapture, StringComparison.Ordinal);
        Assert.Contains($"X-KDE-DBUS-Restricted-Interfaces={KWinScreenShotPermission}", kWinCapture, StringComparison.Ordinal);
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
        var requiredReferencesBySource = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["scripts/packaging/deb/build.sh"] =
            [
                "daemon/crossmacro.service",
                "assets/io.github.alper_han.crossmacro.policy",
                "assets/50-crossmacro.rules",
                "assets/99-crossmacro.rules",
                "assets/crossmacro-modules.conf",
            ],
            ["scripts/packaging/rpm/build.sh"] =
            [
                "daemon/crossmacro.service",
                "assets/io.github.alper_han.crossmacro.policy",
                "assets/50-crossmacro.rules",
                "assets/99-crossmacro.rules",
                "assets/crossmacro-modules.conf",
            ],
            ["scripts/packaging/arch/PKGBUILD"] =
            [
                "scripts/daemon/crossmacro.service",
                "scripts/assets/io.github.alper_han.crossmacro.policy",
                "scripts/assets/50-crossmacro.rules",
                "scripts/assets/99-crossmacro.rules",
                "crossmacro-modules.conf",
            ],
            ["scripts/packaging/arch/PKGBUILD-git.in"] =
            [
                "scripts/daemon/crossmacro.service",
                "scripts/assets/io.github.alper_han.crossmacro.policy",
                "scripts/assets/50-crossmacro.rules",
                "scripts/assets/99-crossmacro.rules",
                "crossmacro-modules.conf",
            ],
            ["scripts/packaging/rpm/crossmacro.spec"] =
            [
                "crossmacro.service",
                "io.github.alper_han.crossmacro.policy",
                "50-crossmacro.rules",
                "99-crossmacro.rules",
                "crossmacro-modules.conf",
            ],
            ["scripts/daemon/install.sh"] =
            [
                "scripts/assets/99-crossmacro.rules",
                "scripts/assets/crossmacro-modules.conf",
                "scripts/assets/io.github.alper_han.crossmacro.policy",
                "scripts/assets/50-crossmacro.rules",
                "crossmacro.service",
            ],
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
    public void LinuxPackages_ShouldDeclareToolsUsedByProvisioningHooks()
    {
        var rpmSpec = ReadRepoFile("scripts/packaging/rpm/crossmacro.spec");
        var debScript = ReadRepoFile("scripts/packaging/deb/build.sh");
        var archPkgbuild = ReadRepoFile("scripts/packaging/arch/PKGBUILD");

        Assert.Contains("Requires(pre): shadow-utils", rpmSpec, StringComparison.Ordinal);
        Assert.Contains("Requires(post): shadow-utils", rpmSpec, StringComparison.Ordinal);
        Assert.Contains("Requires(post): systemd-udev", rpmSpec, StringComparison.Ordinal);

        var debDepends = ExtractDebControlFieldValues(debScript, "Depends");
        Assert.Contains("adduser", debDepends);
        Assert.Contains("passwd", debDepends);
        Assert.Contains("udev", debDepends);
        Assert.Contains("init-system-helpers", debDepends);

        var archDepends = ExtractArchDepends(archPkgbuild);
        Assert.Contains("shadow", archDepends);
        Assert.Contains("systemd", archDepends);
    }

    [Fact]
    public void ArchGitPackage_ShouldTrackGitAndConflictWithStablePackage()
    {
        var gitPkgbuild = ReadRepoFile("scripts/packaging/arch/PKGBUILD-git.in");

        Assert.Contains("pkgname=crossmacro-git", gitPkgbuild, StringComparison.Ordinal);
        Assert.Contains("git+https://github.com/alper-han/CrossMacro.git#commit=@SOURCE_COMMIT@", gitPkgbuild, StringComparison.Ordinal);
        Assert.Contains("conflicts=('crossmacro')", gitPkgbuild, StringComparison.Ordinal);
        Assert.Contains("provides=('crossmacro')", gitPkgbuild, StringComparison.Ordinal);
        Assert.Contains("CrossMacroSourceRevision=\"$source_revision\"", gitPkgbuild, StringComparison.Ordinal);
        Assert.Contains("cd \"$srcdir/crossmacro\"", gitPkgbuild, StringComparison.Ordinal);
        Assert.Contains("pkgver()", gitPkgbuild, StringComparison.Ordinal);
    }

    [Fact]
    public void ArchInstallHook_ShouldReportUserGroupChangesTruthfully()
    {
        var installHook = ReadRepoFile("scripts/packaging/arch/crossmacro.install");

        Assert.DoesNotContain("usermod -aG crossmacro \"$installer_user\" >/dev/null 2>&1 || true", installHook, StringComparison.Ordinal);
        Assert.DoesNotContain("was added to 'crossmacro' group (best effort)", installHook, StringComparison.Ordinal);
        Assert.Contains("elif gpasswd -a \"$installer_user\" crossmacro >/dev/null 2>&1; then", installHook, StringComparison.Ordinal);
        Assert.Contains("installer_user_group_status=\"already_member\"", installHook, StringComparison.Ordinal);
        Assert.Contains("installer_user_group_status=\"added\"", installHook, StringComparison.Ordinal);
        Assert.Contains("installer_user_group_status=\"failed\"", installHook, StringComparison.Ordinal);
        Assert.Contains("installer_user_group_status=\"unknown\"", installHook, StringComparison.Ordinal);
        Assert.Contains("'$installer_user' is already a member of the 'crossmacro' group.", installHook, StringComparison.Ordinal);
        Assert.Contains("'$installer_user' was added to the 'crossmacro' group.", installHook, StringComparison.Ordinal);
        Assert.Contains("Could not add '$installer_user' to the 'crossmacro' group automatically.", installHook, StringComparison.Ordinal);
        Assert.Contains("Could not determine the non-root user who launched the installer.", installHook, StringComparison.Ordinal);
        Assert.Contains("sudo gpasswd -a \\$USER crossmacro", installHook, StringComparison.Ordinal);
        Assert.Contains("log out and log back in, or reboot", installHook, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DaemonInstaller_ShouldReportInstallerGroupMutationResult()
    {
        var installer = ReadRepoFile("scripts/daemon/install.sh");

        Assert.Contains("installer_user_group_status=\"not_attempted\"", installer, StringComparison.Ordinal);
        Assert.Contains(
            "getent passwd \"$SUDO_USER\" >/dev/null 2>&1 && gpasswd -a \"$SUDO_USER\" crossmacro",
            installer,
            StringComparison.Ordinal);
        Assert.Contains("installer_user_group_status=\"added\"", installer, StringComparison.Ordinal);
        Assert.Contains("installer_user_group_status=\"failed\"", installer, StringComparison.Ordinal);
        Assert.Contains("case \"$installer_user_group_status\" in", installer, StringComparison.Ordinal);
        Assert.Contains("sudo gpasswd -a <your-username> crossmacro", installer, StringComparison.Ordinal);
        Assert.Contains("sudo gpasswd -a \\$USER crossmacro", installer, StringComparison.Ordinal);
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

    private static string ExtractSection(string text, string startMarker, string endMarker, bool includeEndMarker = false)
    {
        var startIndex = text.IndexOf(startMarker, StringComparison.Ordinal);
        var endIndex = text.IndexOf(endMarker, startIndex + startMarker.Length, StringComparison.Ordinal);

        Assert.True(startIndex >= 0, $"Could not find '{startMarker}' in packaging hook.");
        Assert.True(endIndex >= 0, $"Could not find '{endMarker}' in packaging hook.");

        var length = endIndex - startIndex + (includeEndMarker ? endMarker.Length : 0);
        return text.Substring(startIndex, length);
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

        return ArchDependencyRegex.Matches(dependsLine)
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

            if (line is "finish-args:")
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

    private static IReadOnlyDictionary<string, string> ReadDesktopEntry(string relativePath)
    {
        return ReadRepoFile(relativePath)
            .Split('\n')
            .Select(line => line.Trim().TrimEnd('\r'))
            .Where(line => line.Length > 0 && line[0] is not '#' and not '[')
            .Select(line => line.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.Ordinal);
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

    [GeneratedRegex("io\\.github\\.alper_han\\.crossmacro\\.input-(?:capture|simulate)", RegexOptions.NonBacktracking)]
    private static partial Regex PolkitActionIdRegex();

    [GeneratedRegex("'(?<dependency>[^']+)'", RegexOptions.ExplicitCapture | RegexOptions.NonBacktracking)]
    private static partial Regex ArchDependencyRegex { get; }
}
