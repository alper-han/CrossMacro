using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Tmds.DBus.Protocol;

namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland.DBus;

public abstract class DbusIntegrationTestBase
{
    protected static readonly TimeSpan SessionBusTimeout = TimeSpan.FromSeconds(5);

    protected static async Task<PrivateDbusSessionBus> CreatePrivateSessionBusAsync()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dbus-daemon",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("--session");
        startInfo.ArgumentList.Add("--nofork");
        startInfo.ArgumentList.Add("--print-address=1");

        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start private dbus-daemon for integration tests.");

        try
        {
            var address = await process.StandardOutput.ReadLineAsync().WaitAsync(SessionBusTimeout);
            if (string.IsNullOrWhiteSpace(address))
            {
                var error = await ReadExitedErrorOutputAsync(process);
                throw new InvalidOperationException("Private dbus-daemon did not publish a bus address." + error);
            }

            return new PrivateDbusSessionBus(address, process);
        }
        catch
        {
            await StopProcessAsync(process);
            throw;
        }
    }

    private static async Task<string> ReadExitedErrorOutputAsync(Process process)
    {
        if (!process.HasExited)
        {
            return string.Empty;
        }

        var error = await process.StandardError.ReadToEndAsync().WaitAsync(TimeSpan.FromSeconds(1));
        return string.IsNullOrWhiteSpace(error) ? string.Empty : $" stderr: {error.Trim()}";
    }

    private static async Task StopProcessAsync(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(2));
        }
        catch (Exception ex) when (ex is InvalidOperationException or TaskCanceledException or TimeoutException)
        {
            Debug.WriteLine(ex);
        }
        finally
        {
            process.Dispose();
        }
    }

    protected sealed class PrivateDbusSessionBus(string address, Process process) : IAsyncDisposable
    {
        public DBusConnection CreateConnection()
        {
            return new DBusConnection(address);
        }

        public async ValueTask DisposeAsync()
        {
            await StopProcessAsync(process);
        }
    }
}
