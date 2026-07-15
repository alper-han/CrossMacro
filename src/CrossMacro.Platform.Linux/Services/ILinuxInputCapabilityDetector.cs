namespace CrossMacro.Platform.Linux.Services;

/// <summary>
/// Detects system capabilities and determines the appropriate input provider mode.
/// This abstraction allows testing and decouples capability detection from factory logic.
/// </summary>
public interface ILinuxInputCapabilityDetector
{
    /// <summary>
    /// Checks if the daemon socket is available for IPC communication.
    /// </summary>
    bool CanConnectToDaemon { get; }

    /// <summary>
    /// Checks if direct /dev/uinput write access is available.
    /// </summary>
    bool CanUseDirectUInput { get; }

    /// <summary>
    /// Checks if at least one /dev/input/event* device is readable.
    /// </summary>
    bool CanReadInputEvents { get; }

    /// <summary>
    /// Determines the appropriate input provider mode based on available capabilities.
    /// Result is cached briefly and refreshed periodically.
    /// </summary>
    InputProviderMode DetermineMode();

    /// <summary>
    /// Returns the currently resolved runtime capability snapshot used by backend selection.
    /// </summary>
    LinuxInputCapabilitySnapshot GetSnapshot();

    /// <summary>
    /// Clears cached probe results after external setup changes device or daemon availability.
    /// </summary>
    void InvalidateCache();
}
