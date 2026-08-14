namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.DBus;

internal sealed class KWinScriptLease : IAsyncDisposable
{
    internal static readonly TimeSpan DefaultOperationTimeout = TimeSpan.FromSeconds(2);
    internal static readonly TimeSpan DefaultCleanupTimeout = TimeSpan.FromSeconds(1);

    private readonly Func<string, string, Task<int>> _loadScriptAsync;
    private readonly Func<int, Task> _runScriptAsync;
    private readonly Func<string, Task> _unloadScriptAsync;
    private readonly Action<Exception>? _onCleanupError;
    private readonly TimeSpan _operationTimeout;
    private readonly TimeSpan _cleanupTimeout;
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private readonly Lock _disposeLock = new();
    private Task? _disposeTask;
    private bool _disposed;
    private bool _loadAttempted;

    public KWinScriptLease(
        DBusConnection connection,
        string pluginName,
        Action<Exception>? onCleanupError = null)
        : this(
            pluginName,
            (filePath, name) => new KWinScriptingClient(connection).LoadScriptAsync(filePath, name),
            scriptId => new KWinScriptClient(connection, scriptId).RunAsync(),
            name => new KWinScriptingClient(connection).UnloadScriptAsync(name),
            DefaultOperationTimeout,
            DefaultCleanupTimeout,
            onCleanupError)
    { }

    internal KWinScriptLease(
        string pluginName,
        Func<string, string, Task<int>> loadScriptAsync,
        Func<int, Task> runScriptAsync,
        Func<string, Task> unloadScriptAsync,
        TimeSpan operationTimeout,
        TimeSpan cleanupTimeout,
        Action<Exception>? onCleanupError = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(pluginName);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(operationTimeout, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(cleanupTimeout, TimeSpan.Zero);

        PluginName = pluginName;
        _loadScriptAsync = loadScriptAsync;
        _runScriptAsync = runScriptAsync;
        _unloadScriptAsync = unloadScriptAsync;
        _operationTimeout = operationTimeout;
        _cleanupTimeout = cleanupTimeout;
        _onCleanupError = onCleanupError;
    }

    internal string PluginName { get; }
    internal KWinScriptHandle? Handle { get; private set; }

    public async Task LoadAndRunAsync(string filePath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        try
        {
            await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                ObjectDisposedException.ThrowIf(_disposed, this);

                _loadAttempted = true;
                var loadTask = _loadScriptAsync(filePath, PluginName);
                int scriptId = await LinuxDbusTransportBoundary
                    .AwaitReplyAsync(loadTask, _operationTimeout, cancellationToken)
                    .ConfigureAwait(false);
                if (scriptId < 0)
                {
                    throw new InvalidOperationException($"KWin rejected script '{PluginName}'.");
                }

                Handle = new KWinScriptHandle(scriptId, PluginName);
                await LinuxDbusTransportBoundary
                    .AwaitReplyAsync(_runScriptAsync(scriptId), _operationTimeout, cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                _ = _operationGate.Release();
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            await DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (_disposeLock)
        {
            _disposeTask ??= DisposeCoreAsync();
            return new ValueTask(_disposeTask);
        }
    }

    private async Task DisposeCoreAsync()
    {
        await _operationGate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_loadAttempted)
            {
                await TryCleanupOperationAsync(() => _unloadScriptAsync(PluginName)).ConfigureAwait(false);
            }

            Handle = null;
        }
        finally
        {
            _ = _operationGate.Release();
        }
    }

    private async Task TryCleanupOperationAsync(Func<Task> operationFactory)
    {
        try
        {
            await LinuxDbusTransportBoundary
                .AwaitReplyAsync(operationFactory(), _cleanupTimeout, CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            if (_onCleanupError is null)
            {
                return;
            }

            try
            {
                _onCleanupError(ex);
            }
            catch (Exception callbackException) when (callbackException is not OutOfMemoryException)
            {
                _ = callbackException;
            }
        }
    }
}
