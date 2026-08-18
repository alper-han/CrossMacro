namespace CrossMacro.Platform.Abstractions.Diagnostics;

public readonly record struct LinuxDirectInputFallbackResult(
    LinuxDirectInputFallbackStatus Status,
    bool CanWriteUInput,
    bool CanReadInputEvents,
    string? Message = null)
{
    public bool IsAvailable => Status is LinuxDirectInputFallbackStatus.Available;

    public static LinuxDirectInputFallbackResult FromAccess(bool canWriteUInput, bool canReadInputEvents)
    {
        if (canWriteUInput && canReadInputEvents)
        {
            return new(LinuxDirectInputFallbackStatus.Available, CanWriteUInput: true, CanReadInputEvents: true);
        }

        var status = !canWriteUInput
            ? LinuxDirectInputFallbackStatus.MissingUInputWriteAccess
            : LinuxDirectInputFallbackStatus.MissingInputEventReadAccess;

        return new(status, canWriteUInput, canReadInputEvents);
    }
}
