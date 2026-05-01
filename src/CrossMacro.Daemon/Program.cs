using System;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using CrossMacro.Daemon.Services;
using CrossMacro.Infrastructure.Linux.Native.Systemd;
using CrossMacro.Infrastructure.Logging;
using Serilog;
using Serilog.Events;

namespace CrossMacro.Daemon;

class Program
{
    static async Task Main(string[] args)
    {
        var logLevel = Environment.GetEnvironmentVariable("CROSSMACRO_LOG_LEVEL") ?? "Information";
        LoggerSetup.Initialize(logLevel, enableFileLogging: false);

        Log.Information("Starting CrossMacro.Daemon...");

        using var cts = new CancellationTokenSource();
        using var sigTermInfo = CreateShutdownSignalRegistration(PosixSignal.SIGTERM, "SIGTERM", cts);
        using var sigIntInfo = CreateShutdownSignalRegistration(PosixSignal.SIGINT, "SIGINT", cts);

        using var sigUsr1Info = PosixSignalRegistration.Create((PosixSignal)10, ctx =>
        {
            ctx.Cancel = true;

            var levelSwitch = LoggerSetup.LevelSwitch;
            if (levelSwitch == null) return;

            if (levelSwitch.MinimumLevel == LogEventLevel.Debug)
            {
                LoggerSetup.SetLogLevel("Information");
                Log.Information("[LogLevel] Switched to Information (send SIGUSR1 again for Debug)");
            }
            else
            {
                LoggerSetup.SetLogLevel("Debug");
                Log.Information("[LogLevel] Switched to Debug (send SIGUSR1 again for Information)");
            }
        });

        void OnProcessExit(object? sender, EventArgs e)
        {
            SystemdNotify.Stopping();
        }

        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;

        try
        {
            ISecurityService security = new SecurityService();
            IVirtualDeviceManager virtualDevice = new VirtualDeviceManager();
            IInputCaptureManager inputCapture = new InputCaptureManager();
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
            Log.Information("Daemon stopping...");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Daemon crashed");
        }
        finally
        {
            AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
            SystemdNotify.Stopping();
            Log.CloseAndFlush();
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
            Log.Information("Received {SignalName}, stopping daemon...", signalName);
            shutdown.Cancel();
        });
    }
}
