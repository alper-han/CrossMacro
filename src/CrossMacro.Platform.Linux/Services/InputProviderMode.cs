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
    /// Use direct /dev/uinput access (requires root or a group that can write /dev/uinput).
    /// </summary>
    Legacy,

    /// <summary>
    /// No usable input backend is available.
    /// </summary>
    None,
}
