namespace CrossMacro.UI;

/// <summary>
/// Receives activation requests from later GUI launches in the same user session.
/// </summary>
internal sealed class SingleInstanceActivationListener : IDisposable
{
    private const int ConnectTimeoutMilliseconds = 500;
    private readonly string _pipeName;
    private readonly Action _activate;
    private readonly CancellationTokenSource _stopping = new();
    private int _disposed;

    private SingleInstanceActivationListener(string pipeName, Action activate)
    {
        _pipeName = pipeName;
        _activate = activate;
        _ = AcceptLoopAsync();
    }

    public static SingleInstanceActivationListener? TryStart(string instanceName, Action activate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceName);
        ArgumentNullException.ThrowIfNull(activate);

        try
        {
            return new SingleInstanceActivationListener(GetPipeName(instanceName), activate);
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
            using var client = new NamedPipeClientStream(
                serverName: ".",
                pipeName: GetPipeName(instanceName),
                direction: PipeDirection.Out,
                options: PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            client.ConnectAsync(timeout.Token).GetAwaiter().GetResult();
            return true;
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or TimeoutException or UnauthorizedAccessException)
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
                var server = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.In,
                    maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await using (server.ConfigureAwait(false))
                {
                    await server.WaitForConnectionAsync(token).ConfigureAwait(false);
                    _activate();
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Expected during shutdown.
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            SerilogLog.Warning(ex, "Single-instance activation listener stopped unexpectedly");
        }
    }

    private static string GetPipeName(string instanceName) => $"{instanceName}.Activation";
}
