namespace CrossMacro.UI;

/// <summary>
/// Receives activation requests from later GUI launches in the same user session.
/// </summary>
internal sealed class SingleInstanceActivationListener : IDisposable
{
    private const int ConnectTimeoutMilliseconds = 500;
    private const int MaxLinuxSocketPathBytes = 107;
    private readonly PipePath _pipePath;
    private readonly Action _activate;
    private readonly CancellationTokenSource _stopping = new();
    private readonly Lock _serverGate = new();
    private NamedPipeServerStream? _server;
    private int _disposed;

    private SingleInstanceActivationListener(PipePath pipePath, Action activate)
    {
        _pipePath = pipePath;
        _activate = activate;
        _ = AcceptLoopAsync();
    }

    public static SingleInstanceActivationListener? TryStart(string instanceName, Action activate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceName);
        ArgumentNullException.ThrowIfNull(activate);

        try
        {
            return new SingleInstanceActivationListener(GetPipePath(instanceName), activate);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            SerilogLog.Warning(ex, "Could not initialize single-instance activation listener");
            return null;
        }
    }

    public static bool TrySignal(string instanceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceName);

        try
        {
            using var timeout = new CancellationTokenSource(ConnectTimeoutMilliseconds);
            using var pipePath = GetPipePath(instanceName);
            using var client = new NamedPipeClientStream(
                serverName: ".",
                pipeName: pipePath.Value,
                direction: PipeDirection.Out,
                options: PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            client.ConnectAsync(timeout.Token).GetAwaiter().GetResult();
            return true;
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or TimeoutException or UnauthorizedAccessException or ArgumentException)
        {
            SerilogLog.Debug(ex, "Could not signal the existing CrossMacro instance");
            return false;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) is not 0)
        {
            return;
        }

        try
        {
            _stopping.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed.
        }
        lock (_serverGate)
        {
            _server?.Dispose();
            _pipePath.Dispose();
        }


        _stopping.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task AcceptLoopAsync()
    {
        var token = _stopping.Token;
        try
        {
            while (!token.IsCancellationRequested)
            {
                NamedPipeServerStream server;
                lock (_serverGate)
                {
                    if (token.IsCancellationRequested) { break; }
                    server = new NamedPipeServerStream(
                        _pipePath.Value,
                        PipeDirection.In,
                        maxNumberOfServerInstances: 1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                    _server = server;
                }
                await using (server.ConfigureAwait(false))
                {
                    await server.WaitForConnectionAsync(token).ConfigureAwait(false);
                    _activate();
                }
            }
        }
        catch (Exception ex) when (token.IsCancellationRequested && ex is OperationCanceledException or ObjectDisposedException or IOException)
        {
            // Expected during shutdown.
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            SerilogLog.Warning(ex, "Single-instance activation listener stopped unexpectedly");
        }
        finally
        {
            _pipePath.Dispose();
        }
    }

    private static PipePath GetPipePath(string instanceName)
    {
        if (OperatingSystem.IsLinux())
        {
            // Independent cache roots own independent runtime locks and must not activate each other.
            instanceName = $"{SingleInstanceGuard.GetUnixLockDirectory()}\0{instanceName}";
        }

        // Unix named pipes are backed by domain sockets with short path limits.
        var pipeName = $"cm-{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(instanceName)).AsSpan(0, 16))}";
        if (OperatingSystem.IsLinux()
            && Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR") is { } runtimeDirectory
            && !string.IsNullOrWhiteSpace(runtimeDirectory)
            && Path.IsPathFullyQualified(runtimeDirectory))
        {
            // Flatpak shares this app-scoped directory, but gives each sandbox a private /tmp.
            var pipeDirectory = Path.Combine(runtimeDirectory, "app", AppConstants.DBusNamespace);
            var pipePath = Path.Combine(pipeDirectory, pipeName);
            _ = Directory.CreateDirectory(pipeDirectory);
            if (Encoding.UTF8.GetByteCount(pipePath) <= MaxLinuxSocketPathBytes)
            {
                return new PipePath(pipePath);
            }

            // Each process may use its own short alias: the socket itself stays in the
            // shared runtime directory, even when Flatpak gives each launch a private /tmp.
            var aliasDirectory = Directory.CreateDirectory(
                Path.Combine("/tmp", $"cm-{Guid.NewGuid():N}"),
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            try
            {
                var alias = Path.Combine(aliasDirectory.FullName, "r");
                _ = Directory.CreateSymbolicLink(alias, pipeDirectory);
                return new PipePath(Path.Combine(alias, pipeName), aliasDirectory);
            }
            catch
            {
                aliasDirectory.Delete(recursive: true);
                throw;
            }
        }

        return new PipePath(pipeName);
    }

    private readonly record struct PipePath(string Value, DirectoryInfo? AliasDirectory = null) : IDisposable
    {
        public void Dispose()
        {
            if (AliasDirectory is null) { return; }
            try
            {
                AliasDirectory.Delete(recursive: true);
            }
            catch (DirectoryNotFoundException)
            {
                // The accept loop and synchronous disposal may both finish cleanup.
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                SerilogLog.Debug(ex, "Could not remove single-instance socket alias");
            }
        }
    }
}
