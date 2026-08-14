
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.DBus;

/// <summary>
/// Linux-only boundary for the Protocol transport layer.
/// Keeps DBus service names, object paths, and session connection creation local to the Linux platform assembly.
/// </summary>
internal static class LinuxDbusTransportBoundary
{
    internal const string TrackerServiceName = "io.github.alper_han.crossmacro.Tracker";
    internal const string TrackerObjectPath = "/io/github/alper_han/crossmacro/Tracker";

    internal static DBusConnection CreateSessionConnection()
    {
        return new DBusConnection(DBusAddress.Session!);
    }

    internal static string GetUniqueDestination(DBusConnection connection)
    {
        var uniqueName = connection.UniqueName;
        if (string.IsNullOrEmpty(uniqueName) || uniqueName[0] != ':')
        {
            throw new InvalidOperationException("D-Bus connection does not have a unique destination after connecting.");
        }

        return uniqueName;
    }

    internal static async Task AwaitReplyAsync(
        Task reply,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        try
        {
            await reply.WaitAsync(timeout, TimeProvider.System, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            ObserveFault(reply);
            throw;
        }
    }

    internal static async Task<T> AwaitReplyAsync<T>(
        Task<T> reply,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        try
        {
            return await reply.WaitAsync(timeout, TimeProvider.System, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            ObserveFault(reply);
            throw;
        }
    }

    private static void ObserveFault(Task reply)
    {
        _ = reply.ContinueWith(
            static faultedTask => _ = faultedTask.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }
}
