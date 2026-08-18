
namespace CrossMacro.Platform.Abstractions;

/// <summary>
/// Service for desktop and workspace management.
/// </summary>
public interface IWorkspaceManagementService
{
    /// <summary>Returns the name of the currently active workspace/desktop, or null if unavailable.</summary>
    public Task<string?> GetActiveWorkspaceAsync(CancellationToken cancellationToken = default);

    /// <summary>Switches to the named workspace/desktop.</summary>
    public Task<bool> SwitchWorkspaceAsync(string workspace, CancellationToken cancellationToken = default);

    /// <summary>Moves the currently active window to the named workspace/desktop.</summary>
    public Task<bool> MoveActiveWindowToWorkspaceAsync(string workspace, CancellationToken cancellationToken = default);

    /// <summary>Moves the window at the given address to the named workspace/desktop.</summary>
    public Task<bool> MoveWindowToWorkspaceByAddressAsync(string address, string workspace, CancellationToken cancellationToken = default);
}
