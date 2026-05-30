namespace CrossMacro.Platform.Abstractions;

public enum MacOSPermissionRequirement
{
    ListenEvent,
    PostEvent,
    Accessibility
}

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
            _ => false
        };
    }
}

public readonly record struct MacOSPermissionPlan(
    bool RequiresListenEvent,
    bool RequiresPostEvent,
    bool RequiresAccessibility)
{
    public static MacOSPermissionPlan ForFlow(
        bool capturesInput,
        bool playsBackInput,
        bool usesAccessibilityFeatures)
    {
        return new MacOSPermissionPlan(
            RequiresListenEvent: capturesInput,
            RequiresPostEvent: playsBackInput,
            RequiresAccessibility: usesAccessibilityFeatures);
    }

    public bool IsSatisfiedBy(MacOSPermissionStatus status)
    {
        return (!RequiresListenEvent || status.IsGranted(MacOSPermissionRequirement.ListenEvent))
            && (!RequiresPostEvent || status.IsGranted(MacOSPermissionRequirement.PostEvent))
            && (!RequiresAccessibility || status.IsGranted(MacOSPermissionRequirement.Accessibility));
    }
}

public interface IMacOSPermissionChecker : IPermissionChecker
{
    MacOSPermissionStatus GetCurrentStatus();
    bool IsPermissionGranted(MacOSPermissionRequirement requirement);
    bool IsListenEventAccessGranted();
    bool IsPostEventAccessGranted();
    bool RequestPermission(MacOSPermissionRequirement requirement);
    bool RequestListenEventAccess();
    bool RequestPostEventAccess();
    void OpenInputMonitoringSettings();
}
