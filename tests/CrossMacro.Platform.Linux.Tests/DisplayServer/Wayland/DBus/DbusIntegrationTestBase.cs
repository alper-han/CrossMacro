
namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland.DBus;

public abstract class DbusIntegrationTestBase
{
    protected static readonly TimeSpan SessionBusTimeout = TimeSpan.FromSeconds(5);

    protected static async Task<PrivateDbusSessionBus> CreatePrivateSessionBusAsync()
    {
        var socketDirectory = Directory.CreateTempSubdirectory("CrossMacroDbus_");
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
        startInfo.ArgumentList.Add($"--address=unix:tmpdir={socketDirectory.FullName}");

        Process? process;
        try
        {
            process = Process.Start(startInfo);
        }
        catch
        {
            DeleteSocketDirectory(socketDirectory);
            throw;
        }

        if (process is null)
        {
            DeleteSocketDirectory(socketDirectory);
            throw new InvalidOperationException("Failed to start private dbus-daemon for integration tests.");
        }

        try
        {
            var address = await process.StandardOutput.ReadLineAsync().WaitAsync(SessionBusTimeout);
            if (string.IsNullOrWhiteSpace(address))
            {
                var error = await ReadExitedErrorOutputAsync(process);
                throw new InvalidOperationException("Private dbus-daemon did not publish a bus address." + error);
            }

            return new PrivateDbusSessionBus(address, process, socketDirectory);
        }
        catch
        {
            await StopProcessAsync(process);
            DeleteSocketDirectory(socketDirectory);
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

    private static void DeleteSocketDirectory(DirectoryInfo socketDirectory)
    {
        try
        {
            if (socketDirectory.Exists)
            {
                socketDirectory.Delete(recursive: true);
            }
        }
        catch (IOException)
        {
            Debug.WriteLine($"Failed to remove private D-Bus socket directory '{socketDirectory.FullName}'.");
        }
        catch (UnauthorizedAccessException)
        {
            Debug.WriteLine($"Access denied while removing private D-Bus socket directory '{socketDirectory.FullName}'.");
        }
    }

    protected sealed class PrivateDbusSessionBus(string address, Process process, DirectoryInfo socketDirectory) : IAsyncDisposable
    {
        public DBusConnection CreateConnection()
        {
            return new DBusConnection(address);
        }

        public async ValueTask DisposeAsync()
        {
            await StopProcessAsync(process);
            DeleteSocketDirectory(socketDirectory);
        }
    }
}
