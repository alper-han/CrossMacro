namespace CrossMacro.Cli;

/// <summary>
/// Controls CLI runtime service behavior based on command lifetime.
/// </summary>
public enum CliRuntimeProfile
{
    /// <summary>
    /// Short-lived commands (play/run/record/etc.) that exit immediately after execution.
    /// </summary>
    OneShot = 0,

    /// <summary>
    /// Long-running command mode (headless) where runtime services remain active.
    /// </summary>
    Persistent = 1
}
