namespace CrossMacro.Platform.Abstractions;

public readonly record struct MacOSPermissionStatus(
    bool ListenEventGranted,
    bool PostEventGranted,
    bool AccessibilityGranted,
    bool ListenEventApiAvailable = true,
    bool PostEventApiAvailable = true)
{
    public bool IsGranted(MacOSPermissionRequirement requirement)
    {
        return requirement switch
        {
            MacOSPermissionRequirement.ListenEvent => ListenEventApiAvailable && ListenEventGranted,
            MacOSPermissionRequirement.PostEvent => PostEventApiAvailable && PostEventGranted,
            MacOSPermissionRequirement.Accessibility => AccessibilityGranted,
            _ => false,
        };
    }
}
