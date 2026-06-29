using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.Linux.DisplayServer.Wayland.DBus;
using Tmds.DBus.Protocol;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal sealed class GnomeWindowManager : IWindowManager, IAsyncDisposable
{
    private DBusConnection? _dbusConnection;
    private GnomeTrackerClient? _trackerClient;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;

    public GnomeWindowManager() { }

    private async Task EnsureInitializedAsync(CancellationToken ct)
    {
        if (_initialized) return;
        await _initLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_initialized) return;
            _dbusConnection = LinuxDbusTransportBoundary.CreateSessionConnection();
            await _dbusConnection.ConnectAsync().AsTask().WaitAsync(ct).ConfigureAwait(false);
            _trackerClient = new GnomeTrackerClient(_dbusConnection);
            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task<WindowInfo?> GetActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var json = await _trackerClient!.GetActiveWindowAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrEmpty(json) || json == "null") return null;
            return JsonSerializer.Deserialize(json, GnomeJsonContext.Default.WindowInfo);
        }
        catch { return null; }
    }

    public async Task<IReadOnlyList<WindowInfo>> GetWindowsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var json = await _trackerClient!.GetWindowsAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrEmpty(json)) return [];
            return JsonSerializer.Deserialize(json, GnomeJsonContext.Default.WindowInfoArray) ?? [];
        }
        catch { return []; }
    }

    public async Task<bool> FocusWindowByAddressAsync(string address, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        return await _trackerClient!.FocusWindowAsync(address).WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> FocusWindowByTitleAsync(string titleSubstring, CancellationToken cancellationToken = default)
    {
        var list = await GetWindowsAsync(cancellationToken).ConfigureAwait(false);
        var match = list.FirstOrDefault(w => w.Title.Contains(titleSubstring, StringComparison.OrdinalIgnoreCase));
        if (match != null) return await FocusWindowByAddressAsync(match.Address, cancellationToken).ConfigureAwait(false);
        return false;
    }

    public async Task<bool> FocusWindowByClassAsync(string classSubstring, CancellationToken cancellationToken = default)
    {
        var list = await GetWindowsAsync(cancellationToken).ConfigureAwait(false);
        var match = list.FirstOrDefault(w => w.Class.Contains(classSubstring, StringComparison.OrdinalIgnoreCase));
        if (match != null) return await FocusWindowByAddressAsync(match.Address, cancellationToken).ConfigureAwait(false);
        return false;
    }

    public async Task<bool> CloseWindowByAddressAsync(string address, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        return await _trackerClient!.CloseWindowAsync(address).WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> CloseWindowByTitleAsync(string titleSubstring, CancellationToken cancellationToken = default)
    {
        var list = await GetWindowsAsync(cancellationToken).ConfigureAwait(false);
        var match = list.FirstOrDefault(w => w.Title.Contains(titleSubstring, StringComparison.OrdinalIgnoreCase));
        if (match != null) return await CloseWindowByAddressAsync(match.Address, cancellationToken).ConfigureAwait(false);
        return false;
    }

    public async Task<bool> MoveActiveWindowAsync(int x, int y, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        return await _trackerClient!.MoveActiveWindowAsync(x, y).WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> ResizeActiveWindowAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        return await _trackerClient!.ResizeActiveWindowAsync(width, height).WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> FullscreenActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        return await _trackerClient!.FullscreenActiveWindowAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> MaximizeActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        return await _trackerClient!.MaximizeActiveWindowAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<bool> FloatActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true); // GNOME is floating by default
    }

    public async Task<bool> CenterActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        return await _trackerClient!.CenterActiveWindowAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<string?> GetActiveWorkspaceAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var ws = await _trackerClient!.GetActiveWorkspaceAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
            return string.IsNullOrEmpty(ws) ? null : ws;
        }
        catch { return null; }
    }

    public async Task<bool> SwitchWorkspaceAsync(string workspace, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        return await _trackerClient!.SwitchWorkspaceAsync(workspace).WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> MoveActiveWindowToWorkspaceAsync(string workspace, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        return await _trackerClient!.MoveActiveWindowToWorkspaceAsync(workspace).WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> MoveWindowToWorkspaceByAddressAsync(string address, string workspace, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        return await _trackerClient!.MoveWindowToWorkspaceByAddressAsync(address, workspace).WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        _dbusConnection?.Dispose();
    }
}

[System.Text.Json.Serialization.JsonSerializable(typeof(WindowInfo))]
[System.Text.Json.Serialization.JsonSerializable(typeof(WindowInfo[]))]
internal sealed partial class GnomeJsonContext : System.Text.Json.Serialization.JsonSerializerContext
{
}
