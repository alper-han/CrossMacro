using System;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Native.Linux;
using Serilog;

namespace CrossMacro.Daemon;

class Program
{
    static async Task Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateLogger();

        Log.Information("Starting CrossMacro.Daemon...");

        using var cts = new CancellationTokenSource();
        
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            Log.Information("Received shutdown signal...");
            SystemdNotify.Stopping();
            cts.Cancel();
        };

        AppDomain.CurrentDomain.ProcessExit += (s, e) =>
        {
            SystemdNotify.Stopping();
        };

        var service = new DaemonService();
        
        try
        {
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
