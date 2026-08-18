namespace CrossMacro.Platform.Abstractions.Diagnostics;

public enum LinuxDirectInputFallbackStatus
{
    Available = 0,
    MissingUInputWriteAccess = 1,
    MissingInputEventReadAccess = 2,
    Unavailable = 3,
    Unknown = 4,
}
