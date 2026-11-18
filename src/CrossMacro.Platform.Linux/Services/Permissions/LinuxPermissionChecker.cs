
namespace CrossMacro.Platform.Linux.Services.Permissions;

/// <summary>
/// Linux-specific implementation of permission checking.
/// Handles /dev/uinput access and accessibility settings.
/// </summary>
public class LinuxPermissionChecker : IPermissionChecker
{
    public bool IsSupported => true;
    public bool RequiresStartupPermissionGate => false;

    public bool IsAccessibilityTrusted() => CheckUInputAccess();

    public ValueTask<bool> CheckUInputAccessAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(CheckUInputAccess());
    }

    public bool CheckUInputAccess()
    {
        try
        {
            return LinuxInputProbeUtilities.HasUInputWriteAccess(LinuxInputProbeUtilities.CanOpenForWrite);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "Error checking uinput permissions");
            return false;
        }
    }

    public void OpenAccessibilitySettings()
    {
        // Permission guidance on Linux is handled via uinput/group/polkit flows, not a universal settings URI.
        Log.Debug("OpenAccessibilitySettings is not applicable on Linux");
    }
}
