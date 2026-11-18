using System.Runtime.InteropServices;
using CrossMacro.Daemon.Logging;
using CrossMacro.Daemon.Security;
using CrossMacro.Daemon.Services.Devices;
using CrossMacro.Daemon.Services.Runtime;
using CrossMacro.Daemon.Services.Sessions;
using CrossMacro.Platform.Linux.Native.Systemd;

namespace CrossMacro.Daemon;

internal static class Program
{
    // SIGUSR1 has no PosixSignal member; PosixSignalRegistration accepts raw Linux signal numbers.
    private const PosixSignal SigUsr1 = (PosixSignal)10;

    private static async Task Main()
    {
        if (!OperatingSystem.IsLinux())
        {
            await Console.Error.WriteLineAsync("CrossMacro.Daemon only runs on Linux (uinput/evdev, Unix domain sockets).").ConfigureAwait(false);
            Environment.ExitCode = 1;
            return;
        }

        var logLevel = Environment.GetEnvironmentVariable("CROSSMACRO_LOG_LEVEL") ?? "Information";
        DaemonLoggerSetup.Initialize(logLevel);

        Log.Information("Starting CrossMacro.Daemon...");

        using var cts = new CancellationTokenSource();
        using var sigTermInfo = CreateShutdownSignalRegistration(PosixSignal.SIGTERM, "SIGTERM", cts);
        using var sigIntInfo = CreateShutdownSignalRegistration(PosixSignal.SIGINT, "SIGINT", cts);

        using var sigUsr1Info = PosixSignalRegistration.Create(SigUsr1, ctx =>
        {
            ctx.Cancel = true;
            var level = DaemonLoggerSetup.ToggleDebugLevel();
            if (level is not null)
            {
                Log.Information("[LogLevel] Switched to {Level} (send SIGUSR1 again to toggle)", level);
            }
        });

        static void OnProcessExit(object? sender, EventArgs e)
        {
            SystemdNotify.Stopping();
        }

        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;

        SecurityService? security = null;
        VirtualDeviceManager? virtualDevice = null;
        InputCaptureManager? inputCapture = null;

        try
        {
            security = new SecurityService(
                new RateLimiter(maxConnectionsPerWindow: 10, windowSeconds: 60, banSeconds: 60),
                new AuditLogger(),
                new PeerCredentialsProvider(),
                new PolkitChecker());
            virtualDevice = new VirtualDeviceManager();
            inputCapture = new InputCaptureManager();
            var service = new DaemonService(
                security,
                new LinuxPermissionService(),
                new SessionHandler(security, virtualDevice, inputCapture),
                new DaemonSocketPathResolver().ResolveSocketPath());

            await service.RunAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Log.Debug("Daemon stopping...");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Fatal(ex, "Daemon crashed");
        }
        finally
        {
            await DisposeOwnedResourcesAsync(inputCapture, virtualDevice, security).ConfigureAwait(false);
            AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
            SystemdNotify.Stopping();
            await SerilogLog.CloseAndFlushAsync().ConfigureAwait(false);
        }
    }

    private static async Task DisposeOwnedResourcesAsync(
        IDisposable? inputCapture,
        IAsyncDisposable? virtualDevice,
        SecurityService? security)
    {
        var errors = new List<Exception>();
        if (inputCapture is not null)
        {
            TryDispose(inputCapture, errors);
        }

        if (virtualDevice is not null)
        {
            try
            {
                await virtualDevice.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                errors.Add(ex);
            }
        }

        if (security is not null)
        {
            try
            {
                await security.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                errors.Add(ex);
            }
        }

        if (errors.Count > 0)
        {
            Log.LogError(new AggregateException("Daemon resource cleanup failed.", errors), "Daemon shutdown cleanup failed");
        }
    }

    private static void TryDispose(IDisposable resource, ICollection<Exception> errors)
    {
        try
        {
            resource.Dispose();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            errors.Add(ex);
        }
    }

    private static PosixSignalRegistration CreateShutdownSignalRegistration(
        PosixSignal signal,
        string signalName,
        CancellationTokenSource shutdown)
    {
        return PosixSignalRegistration.Create(signal, ctx =>
        {
            ctx.Cancel = true;
            Log.Information("Received {SignalName}, stopping daemon...", signalName);
            shutdown.Cancel();
        });
    }
}
