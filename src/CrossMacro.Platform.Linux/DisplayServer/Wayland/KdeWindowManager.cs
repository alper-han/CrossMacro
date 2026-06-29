using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Logging;
using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.Linux.DisplayServer.Wayland.DBus;
using Tmds.DBus.Protocol;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal sealed class KdeWindowManager : IWindowManager, IAsyncDisposable
{
    private DBusConnection? _dbusConnection;
    private KdeTrackerService? _trackerService;
    private KdeTrackerServiceMethodHandler? _trackerHandler;
    private bool _initialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _pendingRequests = new();

    private static readonly string ScriptDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "crossmacro", "scripts");

    public KdeWindowManager()
    {
    }

    private async Task EnsureInitializedAsync(CancellationToken ct)
    {
        if (_initialized) return;

        await _initLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_initialized) return;

            _dbusConnection = LinuxDbusTransportBoundary.CreateSessionConnection();
            await _dbusConnection.ConnectAsync().AsTask().WaitAsync(ct).ConfigureAwait(false);

            _trackerService = new KdeTrackerService((_, _) => {}, (_, _) => {}, "/io/github/alper_han/crossmacro/WindowManager");
            _trackerService.OnWindowDataReceived += (corrId, json) => 
            {
                if (_pendingRequests.TryRemove(corrId, out var tcs))
                {
                    tcs.TrySetResult(json);
                }
            };

            _trackerHandler = new KdeTrackerServiceMethodHandler(_trackerService);
            _dbusConnection.AddMethodHandler(_trackerHandler);

            try 
            {
                await _dbusConnection.RequestNameAsync(KdeTrackerService.TrackerServiceName, RequestNameOptions.ReplaceExisting).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "[KdeWindowManager] RequestNameAsync failed (likely already owned).");
            }

            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private string GetSafeScriptPath(string fileName)
    {
        if (!Directory.Exists(ScriptDirectory))
            Directory.CreateDirectory(ScriptDirectory);
            
        return Path.Combine(ScriptDirectory, fileName);
    }

    private async Task<string?> ExecuteOneShotScriptAsync(string jsContent, bool expectsCallback, CancellationToken ct)
    {
        await EnsureInitializedAsync(ct).ConfigureAwait(false);

        string correlationId = Guid.NewGuid().ToString("N");
        TaskCompletionSource<string>? tcs = null;

        if (expectsCallback)
        {
            tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingRequests[correlationId] = tcs;
        }

        string finalScript = jsContent.Replace("__CORRELATION_ID__", correlationId, StringComparison.Ordinal)
                                      .Replace("__SERVICE_NAME__", KdeTrackerService.TrackerServiceName, StringComparison.Ordinal)
                                      .Replace("__OBJECT_PATH__", "/io/github/alper_han/crossmacro/WindowManager", StringComparison.Ordinal)
                                      .Replace("__INTERFACE__", KdeTrackerService.TrackerInterface, StringComparison.Ordinal);

        string tempJsFile = GetSafeScriptPath($"kwin_wm_{correlationId}.js");
        await File.WriteAllTextAsync(tempJsFile, finalScript, ct).ConfigureAwait(false);

        string? scriptId = null;
        try
        {
            if (_dbusConnection == null) return null;

            var scriptingProxy = new KWinScriptingClient(_dbusConnection);
            var scriptIdInt = await scriptingProxy.LoadScriptAsync(tempJsFile).WaitAsync(ct).ConfigureAwait(false);
            if (scriptIdInt < 0) return null;
            scriptId = scriptIdInt.ToString();

            var scriptProxy = new KWinScriptClient(_dbusConnection, scriptId);
            await scriptProxy.RunAsync().WaitAsync(ct).ConfigureAwait(false);

            if (expectsCallback && tcs != null)
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(5000); 
                try { return await tcs.Task.WaitAsync(timeoutCts.Token).ConfigureAwait(false); }
                catch (TimeoutException) { return null; }
            }
            return "ok";
        }
        catch (Exception) { return null; }
        finally
        {
            if (scriptId != null && _dbusConnection != null)
            {
                try
                {
                    var scriptingProxy = new KWinScriptingClient(_dbusConnection);
                    await scriptingProxy.UnloadScriptAsync(scriptId).ConfigureAwait(false);
                }
                catch { }
            }
            if (File.Exists(tempJsFile))
            {
                try { File.Delete(tempJsFile); } catch { }
            }
            if (expectsCallback) _pendingRequests.TryRemove(correlationId, out _);
        }
    }

    private const string JsCallbackFunction = @"
        function sendCallback(data) {
            callDBus('__SERVICE_NAME__', '__OBJECT_PATH__', '__INTERFACE__', 'ReportWindowData', '__CORRELATION_ID__', JSON.stringify(data));
        }
    ";

    public async Task<WindowInfo?> GetActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        string script = JsCallbackFunction + @"
        (function() {
            var w = workspace.activeWindow || workspace.activeClient;
            if (w) {
                sendCallback({
                    Address: (w.internalId || w.windowId || 0).toString(),
                    Title: w.caption || '',
                    Class: w.resourceClass || '',
                    Pid: w.pid || 0,
                    Workspace: (workspace.currentDesktop && workspace.currentDesktop.name) ? workspace.currentDesktop.name : '',
                    IsFocused: true,
                    IsFullscreen: w.fullScreen || false,
                    IsFloating: w.tile == null,
                    IsPinned: w.onAllDesktops || false,
                    IsHidden: w.minimized || false, X: (w.frameGeometry ? Math.round(w.frameGeometry.x) : 0), Y: (w.frameGeometry ? Math.round(w.frameGeometry.y) : 0), Width: (w.frameGeometry ? Math.round(w.frameGeometry.width) : 0), Height: (w.frameGeometry ? Math.round(w.frameGeometry.height) : 0)
                });
            } else {
                sendCallback(null);
            }
        })();";

        var json = await ExecuteOneShotScriptAsync(script, true, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(json) || json == "null") return null;

        try { return JsonSerializer.Deserialize(json, KdeJsonContext.Default.WindowInfo); }
        catch { return null; }
    }

    public async Task<IReadOnlyList<WindowInfo>> GetWindowsAsync(CancellationToken cancellationToken = default)
    {
        string script = JsCallbackFunction + @"
        (function() {
            var out = [];
            var list = (typeof workspace.windowList === 'function') ? workspace.windowList() : workspace.clientList();
            for (var i = 0; i < list.length; i++) {
                var w = list[i];
                out.push({
                    Address: (w.internalId || w.windowId || i).toString(),
                    Title: w.caption || '',
                    Class: w.resourceClass || '',
                    Pid: w.pid || 0,
                    Workspace: (w.desktops && w.desktops.length > 0) ? w.desktops[0].name : '',
                    IsFocused: (workspace.activeWindow === w),
                    IsFullscreen: w.fullScreen || false,
                    IsFloating: w.tile == null,
                    IsPinned: w.onAllDesktops || false,
                    IsHidden: w.minimized || false, X: (w.frameGeometry ? Math.round(w.frameGeometry.x) : 0), Y: (w.frameGeometry ? Math.round(w.frameGeometry.y) : 0), Width: (w.frameGeometry ? Math.round(w.frameGeometry.width) : 0), Height: (w.frameGeometry ? Math.round(w.frameGeometry.height) : 0)
                });
            }
            sendCallback(out);
        })();";

        var json = await ExecuteOneShotScriptAsync(script, true, cancellationToken).ConfigureAwait(false);
        Log.Information("JSON: {Json}", json); if (string.IsNullOrEmpty(json)) return [];

        try { return JsonSerializer.Deserialize(json, KdeJsonContext.Default.WindowInfoArray) ?? []; }
        catch { return []; }
    }

    public Task<bool> FocusWindowByAddressAsync(string address, CancellationToken cancellationToken = default)
    {
        string script = "(function() { var list = (typeof workspace.windowList === 'function') ? workspace.windowList() : workspace.clientList(); for (var i = 0; i < list.length; i++) { var id = (list[i].internalId || list[i].windowId || i).toString(); if (id === '" + address + "') { workspace.activeWindow = list[i]; break; } } })();";
        return ExecuteMutationAsync(script, cancellationToken);
    }

    public Task<bool> FocusWindowByTitleAsync(string titleSubstring, CancellationToken cancellationToken = default)
    {
        string script = "(function() { var list = (typeof workspace.windowList === 'function') ? workspace.windowList() : workspace.clientList(); for (var i = 0; i < list.length; i++) { if (list[i].caption && list[i].caption.toLowerCase().indexOf('" + titleSubstring.ToLowerInvariant() + "') !== -1) { workspace.activeWindow = list[i]; break; } } })();";
        return ExecuteMutationAsync(script, cancellationToken);
    }

    public Task<bool> FocusWindowByClassAsync(string classSubstring, CancellationToken cancellationToken = default)
    {
        string script = "(function() { var list = (typeof workspace.windowList === 'function') ? workspace.windowList() : workspace.clientList(); for (var i = 0; i < list.length; i++) { if (list[i].resourceClass && list[i].resourceClass.toLowerCase().indexOf('" + classSubstring.ToLowerInvariant() + "') !== -1) { workspace.activeWindow = list[i]; break; } } })();";
        return ExecuteMutationAsync(script, cancellationToken);
    }

    public Task<bool> CloseWindowByAddressAsync(string address, CancellationToken cancellationToken = default)
    {
        string script = "(function() { var list = (typeof workspace.windowList === 'function') ? workspace.windowList() : workspace.clientList(); for (var i = 0; i < list.length; i++) { var id = (list[i].internalId || list[i].windowId || i).toString(); if (id === '" + address + "') { if (typeof list[i].closeWindow === 'function') list[i].closeWindow(); else list[i].close(); break; } } })();";
        return ExecuteMutationAsync(script, cancellationToken);
    }

    public Task<bool> CloseWindowByTitleAsync(string titleSubstring, CancellationToken cancellationToken = default)
    {
        string script = "(function() { var list = (typeof workspace.windowList === 'function') ? workspace.windowList() : workspace.clientList(); for (var i = 0; i < list.length; i++) { if (list[i].caption && list[i].caption.toLowerCase().indexOf('" + titleSubstring.ToLowerInvariant() + "') !== -1) { if (typeof list[i].closeWindow === 'function') list[i].closeWindow(); else list[i].close(); break; } } })();";
        return ExecuteMutationAsync(script, cancellationToken);
    }

    public Task<bool> MoveActiveWindowAsync(int x, int y, CancellationToken cancellationToken = default)
    {
        string script = "(function() { var w = workspace.activeWindow || workspace.activeClient; if (w) { var g = w.frameGeometry; g.x = " + x + "; g.y = " + y + "; w.frameGeometry = g; } })();";
        return ExecuteMutationAsync(script, cancellationToken);
    }

    public Task<bool> ResizeActiveWindowAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        string script = "(function() { var w = workspace.activeWindow || workspace.activeClient; if (w) { var g = w.frameGeometry; g.width = " + width + "; g.height = " + height + "; w.frameGeometry = g; } })();";
        return ExecuteMutationAsync(script, cancellationToken);
    }

    public Task<bool> FullscreenActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        string script = "(function() { var w = workspace.activeWindow || workspace.activeClient; if (w) { w.fullScreen = !w.fullScreen; } })();";
        return ExecuteMutationAsync(script, cancellationToken);
    }

    public Task<bool> FloatActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    public Task<bool> CenterActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        string script = "(function() { var w = workspace.activeWindow || workspace.activeClient; if (w) { var screen = workspace.activeScreen; if (!screen) return; var sg = screen.geometry || workspace.clientArea(0, screen, workspace.currentDesktop); var wg = w.frameGeometry; wg.x = sg.x + (sg.width - wg.width) / 2; wg.y = sg.y + (sg.height - wg.height) / 2; w.frameGeometry = wg; } })();";
        return ExecuteMutationAsync(script, cancellationToken);
    }

    public async Task<string?> GetActiveWorkspaceAsync(CancellationToken cancellationToken = default)
    {
        string script = JsCallbackFunction + @"
        (function() {
            var d = workspace.currentDesktop;
            sendCallback({ name: (d && d.name) ? d.name : '' });
        })();";
        var json = await ExecuteOneShotScriptAsync(script, true, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(json) || json == "null") return null;
        try 
        { 
            var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("name").GetString();
        }
        catch { return null; }
    }

    public Task<bool> SwitchWorkspaceAsync(string workspaceName, CancellationToken cancellationToken = default)
    {
        string script = "(function() { var desktops = workspace.desktops; for (var i = 0; i < desktops.length; i++) { if (desktops[i].name === '" + workspaceName + "') { workspace.currentDesktop = desktops[i]; break; } } })();";
        return ExecuteMutationAsync(script, cancellationToken);
    }

    public Task<bool> MoveActiveWindowToWorkspaceAsync(string workspaceName, CancellationToken cancellationToken = default)
    {
        string script = "(function() { var w = workspace.activeWindow || workspace.activeClient; if (!w) return; var desktops = workspace.desktops; for (var i = 0; i < desktops.length; i++) { if (desktops[i].name === '" + workspaceName + "') { w.desktops = [desktops[i]]; break; } } })();";
        return ExecuteMutationAsync(script, cancellationToken);
    }

    public Task<bool> MoveWindowToWorkspaceByAddressAsync(string address, string workspaceName, CancellationToken cancellationToken = default)
    {
        string script = "(function() { var list = (typeof workspace.windowList === 'function') ? workspace.windowList() : workspace.clientList(); var targetWindow = null; for (var i = 0; i < list.length; i++) { var id = (list[i].internalId || list[i].windowId || i).toString(); if (id === '" + address + "') { targetWindow = list[i]; break; } } if (!targetWindow) return; var desktops = workspace.desktops; for (var i = 0; i < desktops.length; i++) { if (desktops[i].name === '" + workspaceName + "') { targetWindow.desktops = [desktops[i]]; break; } } })();";
        return ExecuteMutationAsync(script, cancellationToken);
    }

    private async Task<bool> ExecuteMutationAsync(string script, CancellationToken ct)
    {
        var result = await ExecuteOneShotScriptAsync(script, expectsCallback: false, ct).ConfigureAwait(false);
        return result == "ok";
    }

    public async ValueTask DisposeAsync()
    {
        if (_trackerHandler != null && _dbusConnection != null)
        {
            _dbusConnection.RemoveMethodHandler(_trackerHandler.Path);
        }
        _dbusConnection?.Dispose();
    }
}

[JsonSerializable(typeof(WindowInfo))]
[JsonSerializable(typeof(WindowInfo[]))]
internal sealed partial class KdeJsonContext : JsonSerializerContext
{
}
