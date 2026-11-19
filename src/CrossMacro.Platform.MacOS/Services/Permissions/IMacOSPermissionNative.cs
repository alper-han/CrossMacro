namespace CrossMacro.Platform.MacOS.Services.Permissions;

/// <summary>The TCC entry points <see cref="MacOSPermissionCheckerService"/> composes; one call per native API.</summary>
internal interface IMacOSPermissionNative
{
    public bool IsAccessibilityTrusted();

    /// <summary>Asks macOS to prompt for Accessibility trust and returns the current trust state.</summary>
    public bool PromptAccessibility();

    public bool PreflightListenEvent();

    public bool RequestListenEventWithIOHID();

    public bool RequestListenEventWithCoreGraphics();

    public bool PreflightPostEvent();

    public bool RequestPostEvent();

    public bool PreflightScreenCapture();

    public bool RequestScreenCapture();

    /// <summary>Opens System Settings at a Privacy &amp; Security pane such as <c>Privacy_Accessibility</c>.</summary>
    public void OpenPrivacySettings(string pane);
}
