namespace CrossMacro.Platform.Abstractions.Input.Simulation;

/// <summary>
/// Provides a compositor-specific reset to the logical desktop origin without
/// advertising support for general absolute playback.
/// </summary>
public interface IDesktopOriginResetSimulator
{
    public bool TryResetToDesktopOrigin();
}
