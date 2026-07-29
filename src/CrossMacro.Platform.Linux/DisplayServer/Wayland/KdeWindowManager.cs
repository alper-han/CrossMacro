
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal sealed class KdeWindowManager : IWindowManager, IAsyncDisposable
{
    private DBusConnection? _dbusConnection;
    private KdeTrackerServiceMethodHandler? _trackerHandler;
    private bool _initialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private readonly SemaphoreSlim _operationLock = new(1, 1);
    private readonly Lock _disposeStateLock = new();
    private Task? _disposeTask;
    private bool _disposed;

    private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _pendingRequests = new(StringComparer.Ordinal);

    private static readonly string ScriptDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "crossmacro", "scripts");

    public KdeWindowManager() { /* Empty */ }

    private async Task EnsureInitializedAsync(CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_initialized)
        {
            return;
        }

        await _initLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_initialized)
            {
                return;
            }

            _dbusConnection = LinuxDbusTransportBoundary.CreateSessionConnection();
            try
            {
                await _dbusConnection.ConnectAsync().AsTask().WaitAsync(ct).ConfigureAwait(false);

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

                try
                {
                    await _dbusConnection.RequestNameAsync("io.github.alper_han.crossmacro.WindowManager", RequestNameOptions.Default).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    Log.Debug(ex, "[KdeWindowManager] RequestNameAsync failed (likely already owned).");
                }

                _initialized = true;
            }
            catch
            {
                _dbusConnection.Dispose();
                _dbusConnection = null;
                _trackerHandler = null;
                throw;
            }
        }
        finally
        {
            _ = _initLock.Release();
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
        if (!Directory.Exists(ScriptDirectory))
        {
            _ = Directory.CreateDirectory(ScriptDirectory);
        }

        return Path.Combine(ScriptDirectory, fileName);
    }

    private async Task<string?> ExecuteOneShotScriptAsync(string jsContent, bool expectsCallback, CancellationToken ct)
    {
        await _operationLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            await EnsureInitializedAsync(ct).ConfigureAwait(false);

            string correlationId = Guid.NewGuid().ToString("N");
            TaskCompletionSource<string>? tcs = null;

            if (expectsCallback)
            {
                tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                _pendingRequests[correlationId] = tcs;
            }

            string finalScript = jsContent.Replace("__CORRELATION_ID__", correlationId, StringComparison.Ordinal)
                                          .Replace("__SERVICE_NAME__", "io.github.alper_han.crossmacro.WindowManager", StringComparison.Ordinal)
                                          .Replace("__OBJECT_PATH__", "/io/github/alper_han/crossmacro/WindowManager", StringComparison.Ordinal)
                                          .Replace("__INTERFACE__", KdeTrackerService.TrackerInterface, StringComparison.Ordinal);

            string tempJsFile = GetSafeScriptPath($"kwin_wm_{correlationId}.js");
            string? scriptId = null;
            try
            {
                await File.WriteAllTextAsync(tempJsFile, finalScript, ct).ConfigureAwait(false);

                var (result, loadedScriptId) = await RunOneShotScriptCoreAsync(tempJsFile, expectsCallback, tcs, ct).ConfigureAwait(false);
                scriptId = loadedScriptId;
                return result;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException) { return null; }
            finally
            {
                await CleanupOneShotScriptAsync(scriptId, tempJsFile, expectsCallback, correlationId).ConfigureAwait(false);
            }
        }
        finally
        {
            _ = _operationLock.Release();
        }
    }

    private async Task<(string? Result, string? ScriptId)> RunOneShotScriptCoreAsync(
        string tempJsFile, bool expectsCallback, TaskCompletionSource<string>? tcs, CancellationToken ct)
    {
        if (_dbusConnection is null)
        {
            return (null, null);
        }

        var scriptingProxy = new KWinScriptingClient(_dbusConnection);
        var scriptIdInt = await scriptingProxy.LoadScriptAsync(tempJsFile).WaitAsync(ct).ConfigureAwait(false);
        if (scriptIdInt < 0)
        {
            return (null, null);
        }

        string scriptId = scriptIdInt.ToString(CultureInfo.InvariantCulture);
        var scriptProxy = new KWinScriptClient(_dbusConnection, scriptId);
        await scriptProxy.RunAsync().WaitAsync(ct).ConfigureAwait(false);

        if (expectsCallback && tcs is not null)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(5000);
            try { return (await tcs.Task.WaitAsync(timeoutCts.Token).ConfigureAwait(false), scriptId); }
            catch (TimeoutException) { return (null, scriptId); }
        }

        return ("ok", scriptId);
    }

    private async Task CleanupOneShotScriptAsync(string? scriptId, string tempJsFile, bool expectsCallback, string correlationId)
    {
        if (scriptId is not null && _dbusConnection is not null)
        {
            try
            {
                var scriptingProxy = new KWinScriptingClient(_dbusConnection);
                await scriptingProxy.UnloadScriptAsync(scriptId).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // Ignore failures while trying to unload script during cleanup
            }
        }

        if (File.Exists(tempJsFile))
        {
            try
            {
                File.Delete(tempJsFile);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // Ignore file delete errors in temp clean up
            }
        }

        if (expectsCallback)
        {
            _ = _pendingRequests.TryRemove(correlationId, out _);
        }
    }

    private const string JsCallbackFunction = "\n        function sendCallback(data) {\n            callDBus('__SERVICE_NAME__', '__OBJECT_PATH__', '__INTERFACE__', 'ReportWindowData', '__CORRELATION_ID__', JSON.stringify(data));\n        }\n    ";

    public async Task<WindowInfo?> GetActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        const string script = JsCallbackFunction + "\n        (function() {\n            var w = workspace.activeWindow || workspace.activeClient;\n            if (w) {\n                sendCallback({\n                    Address: (w.internalId || w.windowId || 0).toString(),\n                    Title: w.caption || '',\n                    Class: w.resourceClass || '',\n                    Pid: w.pid || 0,\n                    Workspace: (workspace.currentDesktop && workspace.currentDesktop.name) ? workspace.currentDesktop.name : '',\n                    IsFocused: true,\n                    IsMaximized: w.maximizeMode !== 0,\n                    IsFullscreen: w.fullScreen || false,\n                    IsFloating: w.tile == null,\n                    IsPinned: w.onAllDesktops || false,\n                    IsHidden: w.minimized || false, X: (w.frameGeometry ? Math.round(w.frameGeometry.x) : 0), Y: (w.frameGeometry ? Math.round(w.frameGeometry.y) : 0), Width: (w.frameGeometry ? Math.round(w.frameGeometry.width) : 0), Height: (w.frameGeometry ? Math.round(w.frameGeometry.height) : 0)\n                });\n            } else {\n                sendCallback(null);\n            }\n        })();";

        var json = await ExecuteOneShotScriptAsync(script, expectsCallback: true, cancellationToken).ConfigureAwait(false);
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
        const string script = JsCallbackFunction + "\n        (function() {\n            var out = [];\n            var list = (typeof workspace.windowList === 'function') ? workspace.windowList() : workspace.clientList();\n            for (var i = 0; i < list.length; i++) {\n                var w = list[i];\n                out.push({\n                    Address: (w.internalId || w.windowId || i).toString(),\n                    Title: w.caption || '',\n                    Class: w.resourceClass || '',\n                    Pid: w.pid || 0,\n                    Workspace: (w.desktops && w.desktops.length > 0) ? w.desktops[0].name : '',\n                    IsFocused: (workspace.activeWindow === w),\n                    IsMaximized: w.maximizeMode !== 0,\n                    IsFullscreen: w.fullScreen || false,\n                    IsFloating: w.tile == null,\n                    IsPinned: w.onAllDesktops || false,\n                    IsHidden: w.minimized || false, X: (w.frameGeometry ? Math.round(w.frameGeometry.x) : 0), Y: (w.frameGeometry ? Math.round(w.frameGeometry.y) : 0), Width: (w.frameGeometry ? Math.round(w.frameGeometry.width) : 0), Height: (w.frameGeometry ? Math.round(w.frameGeometry.height) : 0)\n                });\n            }\n            sendCallback(out);\n        })();";

        var json = await ExecuteOneShotScriptAsync(script, expectsCallback: true, cancellationToken).ConfigureAwait(false);
        Log.Information("JSON: {Json}", json); if (string.IsNullOrEmpty(json))
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

    public Task<bool> FocusWindowByAddressAsync(string address, CancellationToken cancellationToken = default)
    {
        string script = "(function() { var list = (typeof workspace.windowList === 'function') ? workspace.windowList() : workspace.clientList(); for (var i = 0; i < list.length; i++) { var id = (list[i].internalId || list[i].windowId || i).toString(); if (id === " + ToJsStringLiteral(address) + ") { workspace.activeWindow = list[i]; break; } } })();";
        return ExecuteMutationAsync(script, cancellationToken);
    }

    public Task<bool> FocusWindowByTitleAsync(string titleSubstring, CancellationToken cancellationToken = default)
    {
        string script = "(function() { var list = (typeof workspace.windowList === 'function') ? workspace.windowList() : workspace.clientList(); for (var i = 0; i < list.length; i++) { if (list[i].caption && list[i].caption.toUpperCase().indexOf(" + ToJsStringLiteral(titleSubstring.ToUpperInvariant()) + ") !== -1) { workspace.activeWindow = list[i]; break; } } })();";
        return ExecuteMutationAsync(script, cancellationToken);
    }

    public Task<bool> FocusWindowByClassAsync(string classSubstring, CancellationToken cancellationToken = default)
    {
        string script = "(function() { var list = (typeof workspace.windowList === 'function') ? workspace.windowList() : workspace.clientList(); for (var i = 0; i < list.length; i++) { if (list[i].resourceClass && list[i].resourceClass.toUpperCase().indexOf(" + ToJsStringLiteral(classSubstring.ToUpperInvariant()) + ") !== -1) { workspace.activeWindow = list[i]; break; } } })();";
        return ExecuteMutationAsync(script, cancellationToken);
    }

    public Task<bool> CloseWindowByAddressAsync(string address, CancellationToken cancellationToken = default)
    {
        string script = "(function() { var list = (typeof workspace.windowList === 'function') ? workspace.windowList() : workspace.clientList(); for (var i = 0; i < list.length; i++) { var id = (list[i].internalId || list[i].windowId || i).toString(); if (id === " + ToJsStringLiteral(address) + ") { if (typeof list[i].closeWindow === 'function') list[i].closeWindow(); else list[i].close(); break; } } })();";
        return ExecuteMutationAsync(script, cancellationToken);
    }

    public Task<bool> CloseWindowByTitleAsync(string titleSubstring, CancellationToken cancellationToken = default)
    {
        string script = "(function() { var list = (typeof workspace.windowList === 'function') ? workspace.windowList() : workspace.clientList(); for (var i = 0; i < list.length; i++) { if (list[i].caption && list[i].caption.toUpperCase().indexOf(" + ToJsStringLiteral(titleSubstring.ToUpperInvariant()) + ") !== -1) { if (typeof list[i].closeWindow === 'function') list[i].closeWindow(); else list[i].close(); break; } } })();";
        return ExecuteMutationAsync(script, cancellationToken);
    }

    public Task<bool> MoveActiveWindowAsync(int x, int y, CancellationToken cancellationToken = default)
    {
        string script = "(function() { var w = workspace.activeWindow || workspace.activeClient; if (w) { var g = w.frameGeometry; w.frameGeometry = { x: " + x.ToString(CultureInfo.InvariantCulture) + ", y: " + y.ToString(CultureInfo.InvariantCulture) + ", width: g.width, height: g.height }; } })();";
        return ExecuteMutationAsync(script, cancellationToken);
    }

    public Task<bool> ResizeActiveWindowAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        string script = "(function() { var w = workspace.activeWindow || workspace.activeClient; if (w) { var g = w.frameGeometry; w.frameGeometry = { x: g.x, y: g.y, width: " + width.ToString(CultureInfo.InvariantCulture) + ", height: " + height.ToString(CultureInfo.InvariantCulture) + " }; } })();";
        return ExecuteMutationAsync(script, cancellationToken);
    }

    public Task<bool> FullscreenActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        const string script = "(function() { var w = workspace.activeWindow || workspace.activeClient; if (w) { w.fullScreen = !w.fullScreen; } })();";
        return ExecuteMutationAsync(script, cancellationToken);
    }

    public Task<bool> MaximizeActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        const string script = "(function() { var w = workspace.activeWindow || workspace.activeClient; if (w) { if (w.maximizeMode !== 0) w.setMaximize(false, false); else w.setMaximize(true, true); } })();";
        return ExecuteMutationAsync(script, cancellationToken);
    }

    public Task<bool> FloatActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    public Task<bool> CenterActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        const string script = "(function() { var w = workspace.activeWindow || workspace.activeClient; if (w) { var screen = workspace.activeScreen; if (!screen) return; var sg = screen.geometry || workspace.clientArea(0, screen, workspace.currentDesktop); var wg = w.frameGeometry; w.frameGeometry = { x: Math.round(sg.x + (sg.width - wg.width) / 2), y: Math.round(sg.y + (sg.height - wg.height) / 2), width: wg.width, height: wg.height }; } })();";
        return ExecuteMutationAsync(script, cancellationToken);
    }

    public async Task<string?> GetActiveWorkspaceAsync(CancellationToken cancellationToken = default)
    {
        const string script = JsCallbackFunction + "\n        (function() {\n            var d = workspace.currentDesktop;\n            sendCallback({ name: (d && d.name) ? d.name : '' });\n        })();";
        var json = await ExecuteOneShotScriptAsync(script, expectsCallback: true, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(json) || string.Equals(json, "null", StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("name").GetString();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return null; }
    }

    public Task<bool> SwitchWorkspaceAsync(string workspace, CancellationToken cancellationToken = default)
    {
        string script = "(function() { var desktops = workspace.desktops; for (var i = 0; i < desktops.length; i++) { if (desktops[i].name === " + ToJsStringLiteral(workspace) + ") { workspace.currentDesktop = desktops[i]; break; } } })();";
        return ExecuteMutationAsync(script, cancellationToken);
    }

    public Task<bool> MoveActiveWindowToWorkspaceAsync(string workspace, CancellationToken cancellationToken = default)
    {
        string script = "(function() { var w = workspace.activeWindow || workspace.activeClient; if (!w) return; var desktops = workspace.desktops; for (var i = 0; i < desktops.length; i++) { if (desktops[i].name === " + ToJsStringLiteral(workspace) + ") { w.desktops = [desktops[i]]; break; } } })();";
        return ExecuteMutationAsync(script, cancellationToken);
    }

    public Task<bool> MoveWindowToWorkspaceByAddressAsync(string address, string workspace, CancellationToken cancellationToken = default)
    {
        string script = "(function() { var list = (typeof workspace.windowList === 'function') ? workspace.windowList() : workspace.clientList(); var targetWindow = null; for (var i = 0; i < list.length; i++) { var id = (list[i].internalId || list[i].windowId || i).toString(); if (id === " + ToJsStringLiteral(address) + ") { targetWindow = list[i]; break; } } if (!targetWindow) return; var desktops = workspace.desktops; for (var i = 0; i < desktops.length; i++) { if (desktops[i].name === " + ToJsStringLiteral(workspace) + ") { targetWindow.desktops = [desktops[i]]; break; } } })();";
        return ExecuteMutationAsync(script, cancellationToken);
    }

    private async Task<bool> ExecuteMutationAsync(string script, CancellationToken ct)
    {
        var result = await ExecuteOneShotScriptAsync(script, expectsCallback: false, ct).ConfigureAwait(false);
        return string.Equals(result, "ok", StringComparison.Ordinal);
    }

    public ValueTask DisposeAsync()
    {
        lock (_disposeStateLock)
        {
            _disposeTask ??= DisposeCoreAsync();
            return new ValueTask(_disposeTask);
        }
    }

    private async Task DisposeCoreAsync()
    {
        await _operationLock.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            await _initLock.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                if (_trackerHandler is not null && _dbusConnection is not null)
                {
                    _dbusConnection.RemoveMethodHandler(_trackerHandler.Path);
                }
                _dbusConnection?.Dispose();
                _dbusConnection = null;
                _trackerHandler = null;
                _initialized = false;
            }
            finally
            {
                _ = _initLock.Release();
            }

            foreach (var pending in _pendingRequests.Values)
            {
                _ = pending.TrySetCanceled(CancellationToken.None);
            }
            _pendingRequests.Clear();
        }
        finally
        {
            _ = _operationLock.Release();
            _operationLock.Dispose();
            _initLock.Dispose();
        }
    }
}
