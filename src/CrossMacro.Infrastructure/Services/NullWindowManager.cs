using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Logging;
using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Infrastructure.Services;

/// <summary>
/// No-op window manager used on platforms that do not support window management.
/// </summary>
public sealed class NullWindowManager : IWindowManager
{
    private static void WarnUnsupported(string operation) =>
        Log.Warning("[NullWindowManager] Window management is not supported on this platform. Operation: {Op}", operation);

    public Task<WindowInfo?> GetActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        WarnUnsupported(nameof(GetActiveWindowAsync));
        return Task.FromResult<WindowInfo?>(null);
    }

    public Task<IReadOnlyList<WindowInfo>> GetWindowsAsync(CancellationToken cancellationToken = default)
    {
        WarnUnsupported(nameof(GetWindowsAsync));
        return Task.FromResult<IReadOnlyList<WindowInfo>>([]);
    }

    public Task<bool> FocusWindowByAddressAsync(string address, CancellationToken cancellationToken = default)
    {
        WarnUnsupported(nameof(FocusWindowByAddressAsync));
        return Task.FromResult(false);
    }

    public Task<bool> FocusWindowByTitleAsync(string titleSubstring, CancellationToken cancellationToken = default)
    {
        WarnUnsupported(nameof(FocusWindowByTitleAsync));
        return Task.FromResult(false);
    }

    public Task<bool> FocusWindowByClassAsync(string classSubstring, CancellationToken cancellationToken = default)
    {
        WarnUnsupported(nameof(FocusWindowByClassAsync));
        return Task.FromResult(false);
    }

    public Task<bool> CloseWindowByAddressAsync(string address, CancellationToken cancellationToken = default)
    {
        WarnUnsupported(nameof(CloseWindowByAddressAsync));
        return Task.FromResult(false);
    }

    public Task<bool> CloseWindowByTitleAsync(string titleSubstring, CancellationToken cancellationToken = default)
    {
        WarnUnsupported(nameof(CloseWindowByTitleAsync));
        return Task.FromResult(false);
    }


    public Task<bool> MoveActiveWindowAsync(int x, int y, CancellationToken cancellationToken = default)
    {
        WarnUnsupported(nameof(MoveActiveWindowAsync));
        return Task.FromResult(false);
    }

    public Task<bool> ResizeActiveWindowAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        WarnUnsupported(nameof(ResizeActiveWindowAsync));
        return Task.FromResult(false);
    }

    public Task<bool> FullscreenActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        WarnUnsupported(nameof(FullscreenActiveWindowAsync));
        return Task.FromResult(false);
    }

    public Task<bool> FloatActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        WarnUnsupported(nameof(FloatActiveWindowAsync));
        return Task.FromResult(false);
    }

    public Task<bool> CenterActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        WarnUnsupported(nameof(CenterActiveWindowAsync));
        return Task.FromResult(false);
    }

    public Task<string?> GetActiveWorkspaceAsync(CancellationToken cancellationToken = default)
    {
        WarnUnsupported(nameof(GetActiveWorkspaceAsync));
        return Task.FromResult<string?>(null);
    }

    public Task<bool> SwitchWorkspaceAsync(string workspace, CancellationToken cancellationToken = default)
    {
        WarnUnsupported(nameof(SwitchWorkspaceAsync));
        return Task.FromResult(false);
    }

    public Task<bool> MoveActiveWindowToWorkspaceAsync(string workspace, CancellationToken cancellationToken = default)
    {
        WarnUnsupported(nameof(MoveActiveWindowToWorkspaceAsync));
        return Task.FromResult(false);
    }

    public Task<bool> MoveWindowToWorkspaceByAddressAsync(string address, string workspace, CancellationToken cancellationToken = default)
    {
        WarnUnsupported(nameof(MoveWindowToWorkspaceByAddressAsync));
        return Task.FromResult(false);
    }
}
