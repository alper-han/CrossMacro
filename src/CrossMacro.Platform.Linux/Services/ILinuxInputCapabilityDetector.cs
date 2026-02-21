namespace CrossMacro.Platform.Linux.Services;

/// <summary>
/// The mode for input provider creation.
/// </summary>
public enum InputProviderMode
{
    /// <summary>
    /// Use daemon IPC for input operations.
    /// </summary>
    Daemon,
    
    /// <summary>
    /// Use direct /dev/uinput access (requires root or input group).
    /// </summary>
    Legacy
}

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
    /// Determines the appropriate input provider mode based on available capabilities.
    /// Result is cached briefly and refreshed periodically.
    /// </summary>
    InputProviderMode DetermineMode();
}
