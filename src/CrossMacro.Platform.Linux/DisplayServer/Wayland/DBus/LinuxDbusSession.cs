
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.DBus;

internal sealed class LinuxDbusSession : IDisposable
{
    private readonly DBusConnection _connection;

    private LinuxDbusSession(DBusConnection connection)
    {
        _connection = connection;
    }

    public static async Task<LinuxDbusSession> ConnectAsync(CancellationToken cancellationToken = default)
    {
        var connection = LinuxDbusTransportBoundary.CreateSessionConnection();
        try
        {
            await connection.ConnectAsync().AsTask().WaitAsync(cancellationToken).ConfigureAwait(false);
            return new LinuxDbusSession(connection);
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    public GnomeShellExtensionsClient CreateGnomeShellExtensionsClient()
        => new(_connection);

    public GnomeTrackerClient CreateGnomeTrackerClient()
        => new(_connection);

    public KdeKeyboardClient CreateKdeKeyboardClient()
        => new(_connection);

    public KWinScriptingClient CreateKWinScriptingClient()
        => new(_connection);

    public KWinScriptClient CreateKWinScriptClient(string scriptId)
        => new(_connection, scriptId);

    public void Dispose()
    {
        _connection.Dispose();
    }
}
