
namespace CrossMacro.Daemon;

internal static class Program
{
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

        SerilogLog.Information("Starting CrossMacro.Daemon...");

        using var cts = new CancellationTokenSource();
        using var sigTermInfo = CreateShutdownSignalRegistration(PosixSignal.SIGTERM, "SIGTERM", cts);
        using var sigIntInfo = CreateShutdownSignalRegistration(PosixSignal.SIGINT, "SIGINT", cts);

        using var sigUsr1Info = PosixSignalRegistration.Create((PosixSignal)10, ctx =>
        {
            ctx.Cancel = true;

            var levelSwitch = DaemonLoggerSetup.LevelSwitch;
            if (levelSwitch is null)
            {
                return;
            }

            if (levelSwitch.MinimumLevel is LogEventLevel.Debug)
            {
                DaemonLoggerSetup.SetLogLevel("Information");
                SerilogLog.Information("[LogLevel] Switched to Information (send SIGUSR1 again for Debug)");
            }
            else
            {
                DaemonLoggerSetup.SetLogLevel("Debug");
                SerilogLog.Information("[LogLevel] Switched to Debug (send SIGUSR1 again for Information)");
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
            security = new SecurityService();
            virtualDevice = new VirtualDeviceManager();
            inputCapture = new InputCaptureManager();
            ISessionHandlerFactory sessionHandlerFactory = new SessionHandlerFactory(security, virtualDevice, inputCapture);
            ILinuxPermissionService permissionService = new LinuxPermissionService();
            IDaemonSocketPathResolver socketPathResolver = new DaemonSocketPathResolver();
            var service = CreateDaemonService(
                security,
                permissionService,
                socketPathResolver,
                sessionHandlerFactory);

            await service.RunAsync(cts.Token).ConfigureAwait(false);

        }
        catch (OperationCanceledException ex)
        {
            SerilogLog.Information(ex, "Daemon stopping...");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            SerilogLog.Fatal(ex, "Daemon crashed");
        }
        finally
        {
            await DisposeOwnedResourcesAsync(inputCapture, virtualDevice, security).ConfigureAwait(false);
            AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
            SystemdNotify.Stopping();
            await SerilogLog.CloseAndFlushAsync().ConfigureAwait(false);
        }
    }

    internal static void DisposeOwnedResources(
        IDisposable? inputCapture,
        IDisposable? virtualDevice,
        IDisposable? security)
    {
        var errors = new List<Exception>();
        if (inputCapture is not null)
        {
            TryDispose(inputCapture, errors);
        }

        if (virtualDevice is not null)
        {
            TryDispose(virtualDevice, errors);
        }

        if (security is not null)
        {
            TryDispose(security, errors);
        }

        if (errors.Count > 0)
        {
            SerilogLog.Error(new AggregateException("Daemon resource cleanup failed.", errors), "Daemon shutdown cleanup failed");
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
            SerilogLog.Error(new AggregateException("Daemon resource cleanup failed.", errors), "Daemon shutdown cleanup failed");
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

    internal static DaemonService CreateDaemonService(
        ISecurityService security,
        ILinuxPermissionService permissionService,
        IDaemonSocketPathResolver socketPathResolver,
        ISessionHandlerFactory sessionHandlerFactory)
    {
        ArgumentNullException.ThrowIfNull(security);
        ArgumentNullException.ThrowIfNull(permissionService);
        ArgumentNullException.ThrowIfNull(socketPathResolver);
        ArgumentNullException.ThrowIfNull(sessionHandlerFactory);

        var socketPath = socketPathResolver.ResolveSocketPath();
        return new DaemonService(
            security,
            permissionService,
            sessionHandlerFactory,
            socketPath);
    }

    private static PosixSignalRegistration CreateShutdownSignalRegistration(
        PosixSignal signal,
        string signalName,
        CancellationTokenSource shutdown)
    {
        return PosixSignalRegistration.Create(signal, ctx =>
        {
            ctx.Cancel = true;
            SerilogLog.Information("Received {SignalName}, stopping daemon...", signalName);
            shutdown.Cancel();
        });
    }
}
