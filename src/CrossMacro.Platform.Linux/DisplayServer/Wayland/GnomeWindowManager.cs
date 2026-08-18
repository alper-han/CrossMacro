
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal sealed class GnomeWindowManager : IWindowManager, IAsyncDisposable
{
    private DBusConnection? _dbusConnection;
    private GnomeTrackerClient? _trackerClient;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;

    public GnomeWindowManager() { /* Empty */ }

    private async Task EnsureInitializedAsync(CancellationToken ct)
    {
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
            await _dbusConnection.ConnectAsync().AsTask().WaitAsync(ct).ConfigureAwait(false);
            _trackerClient = new GnomeTrackerClient(_dbusConnection);
            _initialized = true;
        }
        finally
        {
            _ = _initLock.Release();
        }
    }

    public async Task<WindowInfo?> GetActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        var client = _trackerClient ?? throw new InvalidOperationException("Tracker client is not initialized.");
        try
        {
            var json = await client.GetActiveWindowAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrEmpty(json) || string.Equals(json, "null", StringComparison.Ordinal))
            {
                return null;
            }

            var win = JsonSerializer.Deserialize(json, GnomeJsonContext.Default.WindowInfo);
            return win is not null ? win with { ProcessName = Helpers.ProcessHelper.GetProcessName(win.Pid) } : null;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return null; }
    }

    public async Task<IReadOnlyList<WindowInfo>> GetWindowsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        var client = _trackerClient ?? throw new InvalidOperationException("Tracker client is not initialized.");
        try
        {
            var json = await client.GetWindowsAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrEmpty(json))
            {
                return [];
            }

            var list = JsonSerializer.Deserialize(json, GnomeJsonContext.Default.WindowInfoArray);
            if (list is null)
            {
                return [];
            }

            return list.Select(static w => w with { ProcessName = Helpers.ProcessHelper.GetProcessName(w.Pid) }).ToArray();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return []; }
    }

    public async Task<bool> FocusWindowByAddressAsync(string address, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        var client = _trackerClient ?? throw new InvalidOperationException("Tracker client is not initialized.");
        return await client.FocusWindowAsync(address).WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> FocusWindowByTitleAsync(string titleSubstring, CancellationToken cancellationToken = default)
    {
        var list = await GetWindowsAsync(cancellationToken).ConfigureAwait(false);
        var match = list.FirstOrDefault(w => w.Title.Contains(titleSubstring, StringComparison.OrdinalIgnoreCase));
        if (match != null)
        {
            return await FocusWindowByAddressAsync(match.Address, cancellationToken).ConfigureAwait(false);
        }

        return false;
    }

    public async Task<bool> FocusWindowByClassAsync(string classSubstring, CancellationToken cancellationToken = default)
    {
        var list = await GetWindowsAsync(cancellationToken).ConfigureAwait(false);
        var match = list.FirstOrDefault(w => w.Class.Contains(classSubstring, StringComparison.OrdinalIgnoreCase));
        if (match != null)
        {
            return await FocusWindowByAddressAsync(match.Address, cancellationToken).ConfigureAwait(false);
        }

        return false;
    }

    public async Task<bool> CloseWindowByAddressAsync(string address, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        var client = _trackerClient ?? throw new InvalidOperationException("Tracker client is not initialized.");
        return await client.CloseWindowAsync(address).WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> CloseWindowByTitleAsync(string titleSubstring, CancellationToken cancellationToken = default)
    {
        var list = await GetWindowsAsync(cancellationToken).ConfigureAwait(false);
        var match = list.FirstOrDefault(w => w.Title.Contains(titleSubstring, StringComparison.OrdinalIgnoreCase));
        if (match != null)
        {
            return await CloseWindowByAddressAsync(match.Address, cancellationToken).ConfigureAwait(false);
        }

        return false;
    }

    public async Task<bool> MoveActiveWindowAsync(int x, int y, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        var client = _trackerClient ?? throw new InvalidOperationException("Tracker client is not initialized.");
        return await client.MoveActiveWindowAsync(x, y).WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> ResizeActiveWindowAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        var client = _trackerClient ?? throw new InvalidOperationException("Tracker client is not initialized.");
        return await client.ResizeActiveWindowAsync(width, height).WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> FullscreenActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        var client = _trackerClient ?? throw new InvalidOperationException("Tracker client is not initialized.");
        return await client.FullscreenActiveWindowAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> MaximizeActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        var client = _trackerClient ?? throw new InvalidOperationException("Tracker client is not initialized.");
        return await client.MaximizeActiveWindowAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<bool> FloatActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true); // GNOME is floating by default
    }

    public async Task<bool> CenterActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        var client = _trackerClient ?? throw new InvalidOperationException("Tracker client is not initialized.");
        return await client.CenterActiveWindowAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<string?> GetActiveWorkspaceAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        var client = _trackerClient ?? throw new InvalidOperationException("Tracker client is not initialized.");
        try
        {
            var ws = await client.GetActiveWorkspaceAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
            return string.IsNullOrEmpty(ws) ? null : ws;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return null; }
    }

    public async Task<bool> SwitchWorkspaceAsync(string workspace, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        var client = _trackerClient ?? throw new InvalidOperationException("Tracker client is not initialized.");
        return await client.SwitchWorkspaceAsync(workspace).WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> MoveActiveWindowToWorkspaceAsync(string workspace, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        var client = _trackerClient ?? throw new InvalidOperationException("Tracker client is not initialized.");
        return await client.MoveActiveWindowToWorkspaceAsync(workspace).WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> MoveWindowToWorkspaceByAddressAsync(string address, string workspace, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        var client = _trackerClient ?? throw new InvalidOperationException("Tracker client is not initialized.");
        return await client.MoveWindowToWorkspaceByAddressAsync(address, workspace).WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        _dbusConnection?.Dispose();
        _initLock.Dispose();
    }
}
