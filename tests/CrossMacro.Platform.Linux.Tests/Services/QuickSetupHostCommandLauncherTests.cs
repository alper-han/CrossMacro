namespace CrossMacro.Platform.Linux.Tests.Services;

public sealed class QuickSetupHostCommandLauncherTests
{
    [LinuxFact]
    public async Task DirectLauncher_WhenPkexecCannotElevate_ExecutesRun0FromResolvedPath()
    {
        using var fixture = new LauncherFixture();
        fixture.AddPkexec();
        fixture.AddRun0();
        var launcher = new DirectPolkitHostCommandLauncher();
        var info = await LauncherFixture.PrepareAsync(launcher);

        // PATH changes after preflight must not replace the chosen executable.
        Environment.SetEnvironmentVariable("PATH", "/nonexistent");
        var result = await fixture.ExecuteAsync(info);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("run0:/\nidentity with spaces", result.Output);
    }

    [LinuxFact]
    public async Task DirectLauncher_WhenPkexecIsUsable_PrefersItWithoutFallbackAfterRefusal()
    {
        using var fixture = new LauncherFixture();
        fixture.AddPkexec("exit 126");
        fixture.AddRun0();
        var launcher = new DirectPolkitHostCommandLauncher(HostCommandProbe.ResolveAsync, (_, _) => ValueTask.FromResult(true));
        var info = await LauncherFixture.PrepareAsync(launcher);

        var result = await fixture.ExecuteAsync(info);

        Assert.Equal(126, result.ExitCode);
        Assert.Equal(string.Empty, result.Output);
    }

    [LinuxFact]
    public async Task DirectLauncher_WhenElevationBecomesUnavailable_DoesNotReusePreviousPreparation()
    {
        using var fixture = new LauncherFixture();
        fixture.AddRun0();
        var launcher = new DirectPolkitHostCommandLauncher();
        _ = await LauncherFixture.PrepareAsync(launcher);
        File.Delete(Path.Combine(fixture.DirectoryPath, "run0"));

        var (info, _) = await launcher.CreateStartInfoAsync("true", new LinuxQuickSetupIdentity("1000", "uid:1000"), TestContext.Current.CancellationToken);
        Assert.Null(info);
    }

    [LinuxFact]
    public async Task FlatpakLauncher_UsesHostPathAndHostWorkingDirectory()
    {
        using var fixture = new LauncherFixture();
        var hostDirectory = Path.Combine(fixture.DirectoryPath, "host");
        Directory.CreateDirectory(hostDirectory);
        fixture.AddRun0(hostDirectory);
        LauncherFixture.AddCommand(fixture.DirectoryPath, "run0", "exit 93");
        LauncherFixture.AddCommand(fixture.DirectoryPath, "flatpak-spawn", $$"""
            test "$1" = --host || exit 94
            shift
            test "$1" = --watch-bus || exit 94
            shift
            case "$1" in --directory=*) cd "${1#--directory=}" || exit 95; shift ;; *) exit 96 ;; esac
            PATH='{{hostDirectory}}'
            export PATH
            exec "$@"
            """);
        var launcher = new FlatpakHostCommandLauncher();
        var info = await LauncherFixture.PrepareAsync(launcher);
        Environment.SetEnvironmentVariable("PATH", "/nonexistent");

        var result = await fixture.ExecuteAsync(info);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("run0:/\nidentity with spaces", result.Output);
    }

    [LinuxFact]
    public async Task CommandResolution_RejectsShellBuiltins()
    {
        using var fixture = new LauncherFixture();
        Assert.Null(await HostCommandProbe.ResolveAsync("echo"));
    }

    [LinuxTheory]
    [InlineData("1000", 0, true)]
    [InlineData("not-a-uid", 0, false)]
    [InlineData("1000", 1, false)]
    public async Task FlatpakSetup_UsesOnlySuccessfulHostIdentityBeforeAuthorization(string hostUid, int probeExitCode, bool accepted)
    {
        using var fixture = new LauncherFixture();
        var hostDirectory = Path.Combine(fixture.DirectoryPath, "host");
        Directory.CreateDirectory(hostDirectory);
        LauncherFixture.AddCommand(hostDirectory, "id", $"printf '%s' '{hostUid}'; exit {probeExitCode.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        // The stub validates identity without elevation or device permission changes.
        var authorizationMarker = Path.Combine(fixture.DirectoryPath, "authorization-invoked");
        LauncherFixture.AddCommand(hostDirectory, "run0", $"printf invoked > '{authorizationMarker}'; for argument do identity=$argument; done; test \"$identity\" = 1000 || exit 90");
        LauncherFixture.AddCommand(fixture.DirectoryPath, "flatpak-spawn", $$"""
            test "$1" = --host || exit 91
            shift
            test "$1" = --watch-bus || exit 92
            shift
            test "$1" = --directory=/ || exit 93
            shift
            cd / || exit 94
            PATH='{{hostDirectory}}'
            export PATH
            exec "$@"
            """);
        var localResolver = new LinuxQuickSetupIdentityResolver(() => 4242, () => "4242 1000 1", _ => ValueTask.FromResult<uint?>(1000));
        var executor = new LinuxQuickSetupExecutor(localResolver);
        var result = await executor.RunAsync(new FlatpakHostCommandLauncher(), LinuxQuickSetupScriptOptions.Strict, "Test", "unexpected", TestContext.Current.CancellationToken);
        Assert.Equal(accepted ? QuickSetupOutcome.Succeeded : QuickSetupOutcome.Failed, result.Outcome);
        Assert.Equal(accepted, File.Exists(authorizationMarker));
    }


    [LinuxTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreparedInvocation_WhenAnotherSelectionIsPending_RemainsExecutable(bool flatpak)
    {
        using var fixture = new LauncherFixture();
        fixture.AddRun0();
        LauncherFixture.AddCommand(fixture.DirectoryPath, "flatpak-spawn", "while test \"$#\" -gt 0; do case \"$1\" in --*) shift ;; *) break ;; esac; done; exec \"$@\"");
        var pending = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var hold = false;
        ValueTask<string?> ResolveAsync(string name, CancellationToken token)
            => hold ? new ValueTask<string?>(pending.Task) : HostCommandProbe.ResolveAsync(name, token);
        IPrivilegedHostCommandLauncher launcher = flatpak
            ? new FlatpakHostCommandLauncher(ResolveAsync, (_, name, token) => ResolveAsync(name, token), (_, _, _) => ValueTask.FromResult(false))
            : new DirectPolkitHostCommandLauncher(ResolveAsync, (_, _) => ValueTask.FromResult(false));
        var info = await LauncherFixture.PrepareAsync(launcher);
        hold = true;
        var otherSelection = launcher.CreateStartInfoAsync("true", new LinuxQuickSetupIdentity("1000", "uid:1000"), TestContext.Current.CancellationToken).AsTask();
        try
        {
            var result = await fixture.ExecuteAsync(info);
            Assert.Equal(0, result.ExitCode);
            Assert.Equal("run0:/\nidentity with spaces", result.Output);
        }
        finally
        {
            pending.SetResult(null);
            _ = await otherSelection;
        }
    }


    private sealed class LauncherFixture : IDisposable
    {
        private readonly string? _previousPath = Environment.GetEnvironmentVariable("PATH");
        public string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), $"crossmacro-launcher-{Guid.NewGuid():N}");

        public LauncherFixture()
        {
            Directory.CreateDirectory(DirectoryPath);
            Environment.SetEnvironmentVariable("PATH", DirectoryPath);
        }

        public void AddPkexec(string body = "exit 92")
        {
            AddCommand(DirectoryPath, "pkexec", body);
            File.SetUnixFileMode(Path.Combine(DirectoryPath, "pkexec"), UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.SetUser);
            AddCommand(DirectoryPath, "stat", "printf '65534\\n'");
        }

        public void AddRun0(string? directory = null)
        {
            directory ??= DirectoryPath;
            AddCommand(directory, "systemd-run", """
                test "${0##*/}" = run0 || exit 90
                directory=
                while test "$#" -gt 0; do
                    case "$1" in
                        --description=*) shift ;;
                        --chdir=*) directory=${1#--chdir=}; shift ;;
                        --*) exit 91 ;;
                        *) break ;;
                    esac
                done
                test "$directory" = / || exit 92
                cd "$directory" || exit 93
                printf 'run0:%s\n' "$PWD"
                exec "$@"
                """);
            File.CreateSymbolicLink(Path.Combine(directory, "run0"), "systemd-run");
        }

        public static void AddCommand(string directory, string name, string body)
        {
            var path = Path.Combine(directory, name);
            File.WriteAllText(path, $"#!/bin/sh\n{body}\n");
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        public static async Task<ProcessStartInfo> PrepareAsync(IPrivilegedHostCommandLauncher launcher)
        {
            var (info, _) = await launcher.CreateStartInfoAsync("printf '%s' \"$1\"", new LinuxQuickSetupIdentity("identity with spaces", "test"), TestContext.Current.CancellationToken);
            return Assert.IsType<ProcessStartInfo>(info);
        }

        public async Task<(int ExitCode, string Output)> ExecuteAsync(ProcessStartInfo info)
        {
            info.WorkingDirectory = DirectoryPath;
            using var process = Process.Start(info)!;
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            _ = await error;
            return (process.ExitCode, await output);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("PATH", _previousPath);
            Directory.Delete(DirectoryPath, recursive: true);
        }
    }
}
