
namespace CrossMacro.Daemon;

class Program
{
    static async Task Main(string[] args)
    {
        var logLevel = Environment.GetEnvironmentVariable("CROSSMACRO_LOG_LEVEL") ?? "Information";
        LoggerSetup.Initialize(logLevel, enableFileLogging: false);

        SerilogLog.Information("Starting CrossMacro.Daemon...");

        using var cts = new CancellationTokenSource();
        using var sigTermInfo = CreateShutdownSignalRegistration(PosixSignal.SIGTERM, "SIGTERM", cts);
        using var sigIntInfo = CreateShutdownSignalRegistration(PosixSignal.SIGINT, "SIGINT", cts);

        using var sigUsr1Info = PosixSignalRegistration.Create((PosixSignal)10, ctx =>
        {
            ctx.Cancel = true;

            var levelSwitch = LoggerSetup.LevelSwitch;
            if (levelSwitch is null) return;

            if (levelSwitch.MinimumLevel is LogEventLevel.Debug)
            {
                LoggerSetup.SetLogLevel("Information");
                SerilogLog.Information("[LogLevel] Switched to Information (send SIGUSR1 again for Debug)");
            }
            else
            {
                LoggerSetup.SetLogLevel("Debug");
                SerilogLog.Information("[LogLevel] Switched to Debug (send SIGUSR1 again for Information)");
            }
        });

        void OnProcessExit(object? sender, EventArgs e)
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

            await service.RunAsync(cts.Token);

        }
        catch (OperationCanceledException)
        {
            SerilogLog.Information("Daemon stopping...");
        }
        catch (Exception ex)
        {
            SerilogLog.Fatal(ex, "Daemon crashed");
        }
        finally
        {
            DisposeOwnedResources(inputCapture, virtualDevice, security);
            AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
            SystemdNotify.Stopping();
            SerilogLog.CloseAndFlush();
        }
    }

    internal static void DisposeOwnedResources(
        IDisposable? inputCapture,
        IDisposable? virtualDevice,
        IDisposable? security)
    {
        var errors = new List<Exception>();
        if (inputCapture is not null) TryDispose(inputCapture, errors);
        if (virtualDevice is not null) TryDispose(virtualDevice, errors);
        if (security is not null) TryDispose(security, errors);

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
        catch (Exception ex)
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
