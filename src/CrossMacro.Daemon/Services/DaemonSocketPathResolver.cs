
namespace CrossMacro.Daemon.Services;

internal sealed class DaemonSocketPathResolver : IDaemonSocketPathResolver
{
    private readonly Func<string, bool> _directoryExists;
    private readonly Action<string> _createDirectory;

    public DaemonSocketPathResolver()
        : this(Directory.Exists, static path => Directory.CreateDirectory(path)) { /* Empty */ }

    internal DaemonSocketPathResolver(
        Func<string, bool> directoryExists,
        Action<string> createDirectory)
    {
        _directoryExists = directoryExists;
        _createDirectory = createDirectory;
    }

    public string ResolveSocketPath()
    {
        const string socketPath = IpcProtocol.DefaultSocketPath;
        var socketDir = Path.GetDirectoryName(socketPath);

        if (!string.IsNullOrEmpty(socketDir))
        {
            if (_directoryExists(socketDir))
            {
                return socketPath;
            }

            try
            {
                _createDirectory(socketDir);
                Log.Information("Created socket directory: {Dir}", socketDir);
                return socketPath;
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                Log.LogError(ex, "Cannot create or access required daemon runtime directory: {Dir}", socketDir);
                throw new InvalidOperationException(
                    $"Cannot resolve daemon socket path because the runtime directory '{socketDir}' is unavailable.",
                    ex);
            }
        }

        throw new InvalidOperationException(
            $"Cannot resolve daemon socket path because the runtime directory for '{socketPath}' is unavailable.");
    }
}
