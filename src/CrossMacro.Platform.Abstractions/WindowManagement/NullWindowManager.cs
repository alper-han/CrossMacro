
namespace CrossMacro.Platform.Abstractions.WindowManagement;

/// <summary>
/// Null-object IWindowManager for environments without window management support.
/// An optional callback reports attempted operations so hosts can surface diagnostics
/// without this assembly taking a logging dependency.
/// </summary>
public sealed class NullWindowManager(Action<string>? warnUnsupported = null) : IWindowManager
{
    public bool IsSupported => false;

    private void WarnUnsupported(string operation) => warnUnsupported?.Invoke(operation);

    public Task<WindowInfo?> GetActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        WarnUnsupported(nameof(GetActiveWindowAsync));
        return Task.FromResult<WindowInfo?>(null);
    }

    public Task<IReadOnlyList<WindowInfo>> GetWindowsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        WarnUnsupported(nameof(GetWindowsAsync));
        return Task.FromResult<IReadOnlyList<WindowInfo>>([]);
    }

    public Task<bool> FocusWindowByAddressAsync(string address, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        WarnUnsupported(nameof(FocusWindowByAddressAsync));
        return Task.FromResult(false);
    }

    public Task<bool> FocusWindowByTitleAsync(string titleSubstring, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        WarnUnsupported(nameof(FocusWindowByTitleAsync));
        return Task.FromResult(false);
    }

    public Task<bool> FocusWindowByClassAsync(string classSubstring, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        WarnUnsupported(nameof(FocusWindowByClassAsync));
        return Task.FromResult(false);
    }

    public Task<bool> CloseWindowByAddressAsync(string address, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        WarnUnsupported(nameof(CloseWindowByAddressAsync));
        return Task.FromResult(false);
    }

    public Task<bool> CloseWindowByTitleAsync(string titleSubstring, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        WarnUnsupported(nameof(CloseWindowByTitleAsync));
        return Task.FromResult(false);
    }

    public Task<bool> MoveActiveWindowAsync(int x, int y, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        WarnUnsupported(nameof(MoveActiveWindowAsync));
        return Task.FromResult(false);
    }

    public Task<bool> ResizeActiveWindowAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        WarnUnsupported(nameof(ResizeActiveWindowAsync));
        return Task.FromResult(false);
    }

    public Task<bool> MaximizeActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        WarnUnsupported(nameof(MaximizeActiveWindowAsync));
        return Task.FromResult(false);
    }

    public Task<bool> FullscreenActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        WarnUnsupported(nameof(FullscreenActiveWindowAsync));
        return Task.FromResult(false);
    }

    public Task<bool> FloatActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        WarnUnsupported(nameof(FloatActiveWindowAsync));
        return Task.FromResult(false);
    }

    public Task<bool> CenterActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        WarnUnsupported(nameof(CenterActiveWindowAsync));
        return Task.FromResult(false);
    }

    public Task<string?> GetActiveWorkspaceAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        WarnUnsupported(nameof(GetActiveWorkspaceAsync));
        return Task.FromResult<string?>(null);
    }

    public Task<bool> SwitchWorkspaceAsync(string workspace, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        WarnUnsupported(nameof(SwitchWorkspaceAsync));
        return Task.FromResult(false);
    }

    public Task<bool> MoveActiveWindowToWorkspaceAsync(string workspace, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        WarnUnsupported(nameof(MoveActiveWindowToWorkspaceAsync));
        return Task.FromResult(false);
    }

    public Task<bool> MoveWindowToWorkspaceByAddressAsync(string address, string workspace, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        WarnUnsupported(nameof(MoveWindowToWorkspaceByAddressAsync));
        return Task.FromResult(false);
    }
}
