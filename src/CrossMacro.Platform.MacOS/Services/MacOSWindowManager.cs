namespace CrossMacro.Platform.MacOS.Services;

[SupportedOSPlatform("macos")]
internal sealed class MacOSWindowManager : IWindowManager, IDisposable
{
    private readonly IMacOSWindowBackend _backend;
    private readonly Func<bool> _isMacOS;

    public MacOSWindowManager()
        : this(new MacOSAccessibilityWindowBackend()) { /* Empty */ }

    internal MacOSWindowManager(
        IMacOSWindowBackend backend,
        Func<bool>? isMacOS = null)
    {
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        _isMacOS = isMacOS ?? OperatingSystem.IsMacOS;
    }

    public bool IsSupported => _isMacOS() && _backend.IsAvailable;

    public Task<WindowInfo?> GetActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(IsSupported ? _backend.GetActiveWindow() : null);
    }

    public Task<IReadOnlyList<WindowInfo>> GetWindowsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<WindowInfo>>(IsSupported ? _backend.GetWindows() : []);
    }

    public Task<bool> FocusWindowByAddressAsync(string address, CancellationToken cancellationToken = default) =>
        MutateAsync(address, _backend.Focus, cancellationToken);

    public async Task<bool> FocusWindowByTitleAsync(string titleSubstring, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(titleSubstring))
        {
            return false;
        }

        var window = await FindWindowAsync(
            window => window.Title.Contains(titleSubstring, StringComparison.OrdinalIgnoreCase),
            cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return window is not null && _backend.Focus(window.Address);
    }

    public async Task<bool> FocusWindowByClassAsync(string classSubstring, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(classSubstring))
        {
            return false;
        }

        var window = await FindWindowAsync(
            window => window.Class.Contains(classSubstring, StringComparison.OrdinalIgnoreCase),
            cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return window is not null && _backend.Focus(window.Address);
    }

    public Task<bool> CloseWindowByAddressAsync(string address, CancellationToken cancellationToken = default) =>
        MutateAsync(address, _backend.Close, cancellationToken);

    public async Task<bool> CloseWindowByTitleAsync(string titleSubstring, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(titleSubstring))
        {
            return false;
        }

        var window = await FindWindowAsync(
            candidate => candidate.Title.Contains(titleSubstring, StringComparison.OrdinalIgnoreCase),
            cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return window is not null && _backend.Close(window.Address);
    }

    public async Task<bool> MoveActiveWindowAsync(int x, int y, CancellationToken cancellationToken = default)
    {
        var active = await GetActiveWindowAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return active is not null && _backend.SetPosition(active.Address, x, y);
    }

    public async Task<bool> ResizeActiveWindowAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        if (width <= 0 || height <= 0)
        {
            return false;
        }

        var active = await GetActiveWindowAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return active is not null && _backend.SetSize(active.Address, width, height);
    }

    public async Task<bool> FullscreenActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        var active = await GetActiveWindowAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return active is not null && _backend.ToggleFullscreen(active.Address);
    }

    public async Task<bool> MaximizeActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        var active = await GetActiveWindowAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return active is not null && _backend.Zoom(active.Address);
    }

    public Task<bool> FloatActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(false);
    }

    public async Task<bool> CenterActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        var active = await GetActiveWindowAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var bounds = active is null ? null : _backend.GetContainingDisplayBounds(active.Address);
        if (active is null || bounds is null)
        {
            return false;
        }

        var x = bounds.Value.X + ((bounds.Value.Width - active.Width) / 2);
        var y = bounds.Value.Y + ((bounds.Value.Height - active.Height) / 2);
        return _backend.SetPosition(active.Address, x, y);
    }

    public Task<string?> GetActiveWorkspaceAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<string?>(null);
    }

    public Task<bool> SwitchWorkspaceAsync(string workspace, CancellationToken cancellationToken = default) =>
        UnsupportedWorkspaceAsync(cancellationToken);

    public Task<bool> MoveActiveWindowToWorkspaceAsync(string workspace, CancellationToken cancellationToken = default) =>
        UnsupportedWorkspaceAsync(cancellationToken);

    public Task<bool> MoveWindowToWorkspaceByAddressAsync(string address, string workspace, CancellationToken cancellationToken = default) =>
        UnsupportedWorkspaceAsync(cancellationToken);

    private async Task<WindowInfo?> FindWindowAsync(Func<WindowInfo, bool> predicate, CancellationToken cancellationToken)
    {
        if (!IsSupported)
        {
            return null;
        }

        var windows = await GetWindowsAsync(cancellationToken).ConfigureAwait(false);
        return windows.FirstOrDefault(predicate);
    }

    private Task<bool> MutateAsync(string address, Func<string, bool> operation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(IsSupported && !string.IsNullOrWhiteSpace(address) && operation(address));
    }

    private static Task<bool> UnsupportedWorkspaceAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(false);
    }

    public void Dispose() => _backend.Dispose();
}
