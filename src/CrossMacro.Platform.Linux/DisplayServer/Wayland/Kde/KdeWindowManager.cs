using System.Collections.Concurrent;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.Kde;

internal sealed class KdeWindowManager : IWindowManager, IAsyncDisposable
{
    private static readonly TimeSpan CallbackTimeout = TimeSpan.FromSeconds(5);

    private DBusConnection? _dbusConnection;
    private KdeTrackerServiceMethodHandler? _trackerHandler;
    private string? _callbackDestination;
    private bool _initialized;
    private readonly SemaphoreSlim _operationLock = new(1, 1);
    private readonly Lock _disposeStateLock = new();
    private Task? _disposeTask;
    private int _disposed;

    private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _pendingRequests = new(StringComparer.Ordinal);

    private static readonly string ScriptDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "crossmacro", "scripts");

    private static readonly string WindowOperationsScript = LoadWindowOperationsScript();

    public KdeWindowManager() { /* Empty */ }

    private async Task EnsureInitializedAsync(CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (_initialized)
        {
            return;
        }

        _dbusConnection = LinuxDbusTransportBoundary.CreateSessionConnection();
        try
        {
            await LinuxDbusTransportBoundary
                .AwaitReplyAsync(_dbusConnection.ConnectAsync().AsTask(), WaylandTimeouts.Operation, ct)
                .ConfigureAwait(false);

            var trackerService = new KdeTrackerService((_, _) => { }, (_, _) => { }, "/io/github/alper_han/crossmacro/WindowManager");
            trackerService.OnWindowDataReceived += (corrId, json) =>
            {
                if (_pendingRequests.TryRemove(corrId, out var tcs))
                {
                    _ = tcs.TrySetResult(json);
                }
            };

            _trackerHandler = new KdeTrackerServiceMethodHandler(trackerService);
            _dbusConnection.AddMethodHandler(_trackerHandler);
            _callbackDestination = LinuxDbusTransportBoundary.GetUniqueDestination(_dbusConnection);
            _initialized = true;
        }
        catch
        {
            _dbusConnection.Dispose();
            _dbusConnection = null;
            _trackerHandler = null;
            _callbackDestination = null;
            throw;
        }
    }

    /// <summary>
    /// Escapes user input as a single-quoted JS string literal; unescaped quotes/backslashes
    /// would allow script injection into the KWin scripting context.
    /// </summary>
    private static string ToJsStringLiteral(string value)
    {
        var builder = new StringBuilder(value.Length + 2);
        _ = builder.Append('\'');
        foreach (var ch in value)
        {
            _ = ch switch
            {
                '\\' => builder.Append("\\\\"),
                '\'' => builder.Append("\\'"),
                '"' => builder.Append("\\\""),
                '\n' => builder.Append("\\n"),
                '\r' => builder.Append("\\r"),
                '\t' => builder.Append("\\t"),
                '\u2028' => builder.Append("\\u2028"),
                '\u2029' => builder.Append("\\u2029"),
                < ' ' => builder.Append("\\u").Append(((int)ch).ToString("x4", CultureInfo.InvariantCulture)),
                _ => builder.Append(ch),
            };
        }

        return builder.Append('\'').ToString();
    }

    private static string GetSafeScriptPath(string fileName)
    {
        _ = Directory.CreateDirectory(ScriptDirectory);

        return Path.Combine(ScriptDirectory, fileName);
    }

    private async Task<string?> ExecuteOneShotScriptAsync(string jsContent, CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        await _operationLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            await EnsureInitializedAsync(ct).ConfigureAwait(false);

            string correlationId = Guid.NewGuid().ToString("N");
            string pluginName = CreateOneShotPluginName(correlationId);
            string? tempJsFile = null;
            KWinScriptLease? scriptLease = null;
            try
            {
                var callbackDestination = _callbackDestination
                    ?? throw new InvalidOperationException("KDE D-Bus callback endpoint is not initialized.");
                string finalScript = BuildOneShotScriptContent(jsContent, correlationId, callbackDestination);
                tempJsFile = GetSafeScriptPath($"kwin_wm_{correlationId}.js");
                var connection = _dbusConnection
                    ?? throw new InvalidOperationException("KDE D-Bus connection is not initialized.");
                scriptLease = new KWinScriptLease(
                    connection,
                    pluginName,
                    ex => Log.Debug(ex, "[KdeWindowManager] Failed to clean up KWin script {PluginName}", pluginName));
                var pending = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                _pendingRequests[correlationId] = pending;

                await File.WriteAllTextAsync(tempJsFile, finalScript, ct).ConfigureAwait(false);
                await scriptLease.LoadAndRunAsync(tempJsFile, ct).ConfigureAwait(false);

                return await AwaitCallbackAsync(pending.Task, CallbackTimeout, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                return null;
            }
            finally
            {
                if (scriptLease is not null)
                {
                    try
                    {
                        await scriptLease.DisposeAsync().ConfigureAwait(false);
                    }
                    catch (Exception ex) when (ex is not OutOfMemoryException)
                    {
                        Log.Debug(ex, "[KdeWindowManager] Unexpected KWin script lease disposal failure");
                    }
                }

                CleanupOneShotArtifacts(tempJsFile, correlationId);
            }
        }
        finally
        {
            _ = _operationLock.Release();
        }
    }

    internal static string BuildOneShotScriptContent(string script, string correlationId, string callbackDestination)
    {
        ArgumentException.ThrowIfNullOrEmpty(script);
        ArgumentException.ThrowIfNullOrEmpty(correlationId);
        ArgumentException.ThrowIfNullOrEmpty(callbackDestination);
        return script.Replace("__CORRELATION_ID__", correlationId, StringComparison.Ordinal)
                     .Replace("__SERVICE_NAME__", callbackDestination, StringComparison.Ordinal)
                     .Replace("__OBJECT_PATH__", "/io/github/alper_han/crossmacro/WindowManager", StringComparison.Ordinal)
                     .Replace("__INTERFACE__", KdeTrackerService.TrackerInterface, StringComparison.Ordinal);
    }

    internal static string CreateOneShotPluginName(string correlationId)
    {
        ArgumentException.ThrowIfNullOrEmpty(correlationId);
        return $"crossmacro-window-{correlationId}";
    }

    internal static async Task<string?> AwaitCallbackAsync(
        Task<string> callbackTask,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(callbackTask);
        try
        {
            return await callbackTask.WaitAsync(timeout, TimeProvider.System, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            return null;
        }
    }

    private void CleanupOneShotArtifacts(string? tempJsFile, string correlationId)
    {
        if (tempJsFile is not null)
        {
            try
            {
                File.Delete(tempJsFile);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Log.Debug(ex, "[KdeWindowManager] Failed to delete temporary KWin script {File}", tempJsFile);
            }
        }

        _ = _pendingRequests.TryRemove(correlationId, out _);
    }

    private static string LoadWindowOperationsScript()
    {
        const string resourceName = "CrossMacro.Platform.Linux.DisplayServer.Wayland.Kde.KdeWindowOperations.js";
        using var stream = typeof(KdeWindowManager).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// The one-shot script that runs <paramref name="operation"/> of the embedded script with
    /// <paramref name="jsArguments"/>, which must already be valid JavaScript literals.
    /// </summary>
    internal static string BuildOperationScript(string operation, params string[] jsArguments)
    {
        ArgumentException.ThrowIfNullOrEmpty(operation);
        ArgumentNullException.ThrowIfNull(jsArguments);
        return $"{WindowOperationsScript}\ncrossmacroRun({ToJsStringLiteral(operation)}, [{string.Join(", ", jsArguments)}]);\n";
    }

    /// <summary>A mutation succeeded only when the script itself reported that it acted on its target.</summary>
    internal static bool IsSuccessfulMutationReply(string? reply) => string.Equals(reply, "true", StringComparison.Ordinal);

    private static string ToJsNumber(int value) => value.ToString(CultureInfo.InvariantCulture);

    public async Task<WindowInfo?> GetActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        var json = await ExecuteOneShotScriptAsync(BuildOperationScript("activeWindow"), cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(json) || string.Equals(json, "null", StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            var win = JsonSerializer.Deserialize(json, KdeJsonContext.Default.WindowInfo);
            return win is not null ? win with { ProcessName = Helpers.ProcessHelper.GetProcessName(win.Pid) } : null;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return null; }
    }

    public async Task<IReadOnlyList<WindowInfo>> GetWindowsAsync(CancellationToken cancellationToken = default)
    {
        var json = await ExecuteOneShotScriptAsync(BuildOperationScript("windows"), cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(json))
        {
            return [];
        }

        try
        {
            var list = JsonSerializer.Deserialize(json, KdeJsonContext.Default.WindowInfoArray);
            if (list is null)
            {
                return [];
            }

            return list.Select(static w => w with { ProcessName = Helpers.ProcessHelper.GetProcessName(w.Pid) }).ToArray();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return []; }
    }

    public Task<bool> FocusWindowByAddressAsync(string address, CancellationToken cancellationToken = default) =>
        ExecuteMutationAsync("focusByAddress", cancellationToken, ToJsStringLiteral(address));

    public Task<bool> FocusWindowByTitleAsync(string titleSubstring, CancellationToken cancellationToken = default) =>
        ExecuteMutationAsync("focusByTitle", cancellationToken, ToJsStringLiteral(titleSubstring.ToUpperInvariant()));

    public Task<bool> FocusWindowByClassAsync(string classSubstring, CancellationToken cancellationToken = default) =>
        ExecuteMutationAsync("focusByClass", cancellationToken, ToJsStringLiteral(classSubstring.ToUpperInvariant()));

    public Task<bool> CloseWindowByAddressAsync(string address, CancellationToken cancellationToken = default) =>
        ExecuteMutationAsync("closeByAddress", cancellationToken, ToJsStringLiteral(address));

    public Task<bool> CloseWindowByTitleAsync(string titleSubstring, CancellationToken cancellationToken = default) =>
        ExecuteMutationAsync("closeByTitle", cancellationToken, ToJsStringLiteral(titleSubstring.ToUpperInvariant()));

    public Task<bool> MoveActiveWindowAsync(int x, int y, CancellationToken cancellationToken = default) =>
        ExecuteMutationAsync("moveActive", cancellationToken, ToJsNumber(x), ToJsNumber(y));

    public Task<bool> ResizeActiveWindowAsync(int width, int height, CancellationToken cancellationToken = default) =>
        ExecuteMutationAsync("resizeActive", cancellationToken, ToJsNumber(width), ToJsNumber(height));

    public Task<bool> FullscreenActiveWindowAsync(CancellationToken cancellationToken = default) =>
        ExecuteMutationAsync("fullscreenActive", cancellationToken);

    public Task<bool> MaximizeActiveWindowAsync(CancellationToken cancellationToken = default) =>
        ExecuteMutationAsync("maximizeActive", cancellationToken);

    public Task<bool> FloatActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    public Task<bool> CenterActiveWindowAsync(CancellationToken cancellationToken = default) =>
        ExecuteMutationAsync("centerActive", cancellationToken);

    public async Task<string?> GetActiveWorkspaceAsync(CancellationToken cancellationToken = default)
    {
        var json = await ExecuteOneShotScriptAsync(BuildOperationScript("activeWorkspace"), cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(json) || string.Equals(json, "null", StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("name").GetString();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return null; }
    }

    public Task<bool> SwitchWorkspaceAsync(string workspace, CancellationToken cancellationToken = default) =>
        ExecuteMutationAsync("switchWorkspace", cancellationToken, ToJsStringLiteral(workspace));

    public Task<bool> MoveActiveWindowToWorkspaceAsync(string workspace, CancellationToken cancellationToken = default) =>
        ExecuteMutationAsync("moveActiveToWorkspace", cancellationToken, ToJsStringLiteral(workspace));

    public Task<bool> MoveWindowToWorkspaceByAddressAsync(string address, string workspace, CancellationToken cancellationToken = default) =>
        ExecuteMutationAsync("moveToWorkspaceByAddress", cancellationToken, ToJsStringLiteral(address), ToJsStringLiteral(workspace));

    private async Task<bool> ExecuteMutationAsync(string operation, CancellationToken ct, params string[] jsArguments)
    {
        var reply = await ExecuteOneShotScriptAsync(BuildOperationScript(operation, jsArguments), ct).ConfigureAwait(false);
        return IsSuccessfulMutationReply(reply);
    }

    public ValueTask DisposeAsync()
    {
        lock (_disposeStateLock)
        {
            if (_disposeTask is null)
            {
                Volatile.Write(ref _disposed, 1);
                _disposeTask = DisposeCoreAsync();
            }

            return new ValueTask(_disposeTask);
        }
    }

    internal bool IsDisposed => Volatile.Read(ref _disposed) is not 0;

    private async Task DisposeCoreAsync()
    {
        await _operationLock.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            if (_trackerHandler is not null && _dbusConnection is not null)
            {
                _dbusConnection.RemoveMethodHandler(_trackerHandler.Path);
            }
            _dbusConnection?.Dispose();
            _dbusConnection = null;
            _trackerHandler = null;
            _callbackDestination = null;
            _initialized = false;

            foreach (var pending in _pendingRequests.Values)
            {
                _ = pending.TrySetCanceled(CancellationToken.None);
            }
            _pendingRequests.Clear();
        }
        finally
        {
            _ = _operationLock.Release();
        }
    }
}
