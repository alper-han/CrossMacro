using System.Net;

namespace CrossMacro.Platform.Linux.Tests.Services;

public sealed class FlatpakHostCommandLifecycleTests
{
    [LinuxTheory]
    [InlineData("launcher")]
    [InlineData("resolve")]
    [InlineData("pkexec")]
    [InlineData("identity")]
    public async Task Cancellation_DisconnectsTransportAndStopsUnprivilegedHostChild(string operation)
    {
        await using var transport = await HostTransportFixture.CreateAsync(replyToIdentityProbe: operation is "launcher");
        using var cancellation = new CancellationTokenSource();
        Task running;
        if (operation is "launcher")
        {
            var launcher = new FlatpakHostCommandLauncher(
                (_, _) => ValueTask.FromResult<string?>(transport.SpawnPath),
                (_, name, _) => ValueTask.FromResult<string?>(name is "run0" ? "/controlled/run0" : null),
                (_, _, _) => ValueTask.FromResult(false));
            var executor = new LinuxQuickSetupExecutor(new LinuxQuickSetupIdentityResolver(() => 1000, () => "0 0 4294967295", _ => ValueTask.FromResult<uint?>(null)));
            running = executor.RunAsync(launcher, LinuxQuickSetupScriptOptions.Strict, "Test", "unexpected", cancellation.Token);
        }
        else if (operation is "resolve")
        {
            var probe = HostCommandProbe.ResolveOnHostViaFlatpakSpawnAsync(transport.SpawnPath, "run0", cancellation.Token);
            running = probe.AsTask();
        }
        else if (operation is "pkexec")
        {
            var probe = HostCommandProbe.PkexecIsUsableOnHostViaFlatpakSpawnAsync(transport.SpawnPath, "/controlled/pkexec", cancellation.Token);
            running = probe.AsTask();
        }
        else
        {
            var probe = HostCommandProbe.ReadUidOnHostViaFlatpakSpawnAsync(transport.SpawnPath, cancellation.Token);
            running = probe.AsTask();
        }

        try
        {
            await transport.HostStarted.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, TestContext.Current.CancellationToken);
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
            await transport.Disconnected.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, TestContext.Current.CancellationToken);
            Assert.True(transport.HostChild!.HasExited, "An unprivileged host child must not survive loss of the local transport.");
        }
        finally
        {
            cancellation.Cancel();
        }
    }

    [LinuxFact]
    public async Task ProbeTimeout_ReturnsUnavailableAndStopsLocalAndHostChildren()
    {
        await using var transport = await HostTransportFixture.CreateAsync(replyToIdentityProbe: false);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var running = HostCommandProbe.ResolveOnHostViaFlatpakSpawnAsync(transport.SpawnPath, "run0", cancellation.Token).AsTask();
        try
        {
            await transport.HostStarted.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, TestContext.Current.CancellationToken);
            Assert.Null(await running.WaitAsync(TimeSpan.FromSeconds(10), TimeProvider.System, TestContext.Current.CancellationToken));
            await transport.Disconnected.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, TestContext.Current.CancellationToken);
            Assert.True(transport.HostChild!.HasExited, "A timed-out probe must not leave its unprivileged host child running.");
        }
        finally
        {
            cancellation.Cancel();
            try { await running; }
            catch (OperationCanceledException) { }
        }
    }

    // A controlled executable transport with an independently owned host child: killing
    // the client's local process tree cannot reach this child. The connection lifetime
    // models HostCommand WATCH_BUS without real D-Bus, authorization, or device access.
    private sealed class HostTransportFixture : IAsyncDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), $"crossmacro-host-lifetime-{Guid.NewGuid():N}");
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _lifetime = new();
        private readonly TaskCompletionSource _hostStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string SpawnPath => Path.Combine(_directory, "flatpak-spawn");
        public Process? HostChild { get; private set; }
        public Task HostStarted => _hostStarted.Task;
        public Task Disconnected { get; private set; } = Task.CompletedTask;

        public static async Task<HostTransportFixture> CreateAsync(bool replyToIdentityProbe)
        {
            var bash = await HostCommandProbe.ResolveAsync("bash", TestContext.Current.CancellationToken);
            Assert.SkipUnless(bash is not null, "The controlled transport requires bash /dev/tcp support.");
            var fixture = new HostTransportFixture();
            Directory.CreateDirectory(fixture._directory);
            fixture._listener.Start();
            var port = ((IPEndPoint)fixture._listener.LocalEndpoint).Port.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var identityReply = replyToIdentityProbe ? "case \"$*\" in *crossmacro-identity-probe*) printf '1000\\n'; exit 0 ;; esac" : string.Empty;
            File.WriteAllText(fixture.SpawnPath, $$"""
                #!{{bash}}
                {{identityReply}}
                watch=0
                while test "$#" -gt 0; do
                    case "$1" in
                        --host) shift ;;
                        --watch-bus) watch=1; shift ;;
                        --directory=/) cd / || exit 91; shift ;;
                        --*) exit 92 ;;
                        *) break ;;
                    esac
                done
                (
                    exec 3<>/dev/tcp/127.0.0.1/{{port}}
                    printf '%s\n' "$watch" >&3
                    read -r response <&3
                ) &
                wait
                """);
            File.SetUnixFileMode(fixture.SpawnPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            fixture.Disconnected = fixture.ServeAsync();
            return fixture;
        }

        private async Task ServeAsync()
        {
            using var connection = await _listener.AcceptTcpClientAsync(_lifetime.Token);
            using var reader = new StreamReader(connection.GetStream());
            var watch = await reader.ReadLineAsync(_lifetime.Token);
            HostChild = Process.Start(new ProcessStartInfo("/bin/sh")
            {
                UseShellExecute = false,
                ArgumentList = { "-c", "exec sleep 30" },
            })!;
            _hostStarted.TrySetResult();
            _ = await reader.ReadLineAsync(_lifetime.Token);
            if (watch is "1")
            {
                HostChild.Kill(entireProcessTree: true);
                await HostChild.WaitForExitAsync(_lifetime.Token);
            }
        }

        public async ValueTask DisposeAsync()
        {
            _lifetime.Cancel();
            _listener.Stop();
            try { await Disconnected; }
            catch (OperationCanceledException) { }
            if (HostChild is not null)
            {
                if (!HostChild.HasExited)
                {
                    HostChild.Kill(entireProcessTree: true);
                    await HostChild.WaitForExitAsync();
                }
                HostChild.Dispose();
            }
            _lifetime.Dispose();
            Directory.Delete(_directory, recursive: true);
        }
    }
}
