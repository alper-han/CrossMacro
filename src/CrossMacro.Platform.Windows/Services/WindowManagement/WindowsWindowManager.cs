namespace CrossMacro.Platform.Windows.Services.WindowManagement;

/// <summary>Window matching and cancellation policy over the native Windows backend.</summary>
public sealed class WindowsWindowManager : IWindowManager
{
    private readonly IWindowsWindowBackend _backend;

    public WindowsWindowManager() : this(new WindowsWindowBackend()) { }

    internal WindowsWindowManager(IWindowsWindowBackend backend)
    {
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
    }

    public bool IsSupported => _backend.IsSupported;

    public Task<WindowInfo?> GetActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _backend.GetActiveWindowAsync(cancellationToken);
    }

    public Task<IReadOnlyList<WindowInfo>> GetWindowsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _backend.GetWindowsAsync(cancellationToken);
    }

    public Task<bool> FocusWindowByAddressAsync(string address, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _backend.FocusWindowByAddressAsync(address, cancellationToken);
    }

    public Task<bool> CloseWindowByAddressAsync(string address, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _backend.CloseWindowByAddressAsync(address, cancellationToken);
    }

    public Task<bool> MoveActiveWindowAsync(int x, int y, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _backend.MoveActiveWindowAsync(x, y, cancellationToken);
    }

    public Task<bool> ResizeActiveWindowAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _backend.ResizeActiveWindowAsync(width, height, cancellationToken);
    }

    public Task<bool> FullscreenActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _backend.FullscreenActiveWindowAsync(cancellationToken);
    }

    public Task<bool> MaximizeActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _backend.MaximizeActiveWindowAsync(cancellationToken);
    }

    public Task<bool> FloatActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _backend.FloatActiveWindowAsync(cancellationToken);
    }

    public Task<bool> CenterActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _backend.CenterActiveWindowAsync(cancellationToken);
    }

    public Task<string?> GetActiveWorkspaceAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _backend.GetActiveWorkspaceAsync(cancellationToken);
    }

    public Task<bool> SwitchWorkspaceAsync(string workspace, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _backend.SwitchWorkspaceAsync(workspace, cancellationToken);
    }

    public Task<bool> MoveActiveWindowToWorkspaceAsync(string workspace, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _backend.MoveActiveWindowToWorkspaceAsync(workspace, cancellationToken);
    }

    public Task<bool> MoveWindowToWorkspaceByAddressAsync(string address, string workspace, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _backend.MoveWindowToWorkspaceByAddressAsync(address, workspace, cancellationToken);
    }

    public Task<bool> FocusWindowByTitleAsync(string titleSubstring, CancellationToken cancellationToken = default) =>
        MatchAndExecuteAsync(titleSubstring, matchClass: false, close: false, cancellationToken);

    public Task<bool> FocusWindowByClassAsync(string classSubstring, CancellationToken cancellationToken = default) =>
        MatchAndExecuteAsync(classSubstring, matchClass: true, close: false, cancellationToken);

    public Task<bool> CloseWindowByTitleAsync(string titleSubstring, CancellationToken cancellationToken = default) =>
        MatchAndExecuteAsync(titleSubstring, matchClass: false, close: true, cancellationToken);

    private async Task<bool> MatchAndExecuteAsync(string substring, bool matchClass, bool close, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsSupported || string.IsNullOrWhiteSpace(substring)) { return false; }
        var window = await _backend.FindWindowAsync(
            info => (matchClass ? info.Class : info.Title).Contains(substring, StringComparison.OrdinalIgnoreCase),
            cancellationToken).ConfigureAwait(false);
        if (window is null) { return false; }
        return close
            ? await _backend.CloseWindowByAddressAsync(window.Address, cancellationToken).ConfigureAwait(false)
            : await _backend.FocusWindowByAddressAsync(window.Address, cancellationToken).ConfigureAwait(false);
    }
}
