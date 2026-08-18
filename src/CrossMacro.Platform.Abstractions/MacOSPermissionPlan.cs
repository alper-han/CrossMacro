namespace CrossMacro.Platform.Abstractions;

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
