using System;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using CrossMacro.Daemon.Services;
using CrossMacro.Platform.Linux.Native.Systemd;
using CrossMacro.Core.Logging;
using Serilog;

namespace CrossMacro.Daemon;

class Program
{
    static async Task Main(string[] args)
    {
        // Use shared logger setup with environment variable support
        var logLevel = Environment.GetEnvironmentVariable("CROSSMACRO_LOG_LEVEL") ?? "Information";
        LoggerSetup.Initialize(logLevel);

        Log.Information("Starting CrossMacro.Daemon...");

        using var cts = new CancellationTokenSource();
        
        // Handle SIGTERM (Systemd stop) and SIGINT (Ctrl+C)
        using var sigTermInfo = PosixSignalRegistration.Create(PosixSignal.SIGTERM, ctx => 
        {
            ctx.Cancel = true;
            Log.Information("Received SIGTERM, stopping daemon...");
            SystemdNotify.Stopping();
            cts.Cancel();
        });
        
        using var sigIntInfo = PosixSignalRegistration.Create(PosixSignal.SIGINT, ctx => 
        {
            ctx.Cancel = true;
            Log.Information("Received SIGINT, stopping daemon...");
            SystemdNotify.Stopping();
            cts.Cancel();
        });

        AppDomain.CurrentDomain.ProcessExit += (s, e) =>
        {
            SystemdNotify.Stopping();
        };

        try
        {
            // Service Composition Root (Manual DI)
            ISecurityService security = new SecurityService();
            IVirtualDeviceManager virtualDevice = new VirtualDeviceManager();
            IInputCaptureManager inputCapture = new InputCaptureManager();
            ILinuxPermissionService permissionService = new LinuxPermissionService();
            
            var service = new DaemonService(security, virtualDevice, inputCapture, permissionService);
            
            // Signal systemd that we're ready before starting the main loop
            // This is done inside RunAsync after socket is bound
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
            SystemdNotify.Stopping();
            Log.CloseAndFlush();
        }
    }
}
