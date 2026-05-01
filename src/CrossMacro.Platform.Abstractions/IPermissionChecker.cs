namespace CrossMacro.Platform.Abstractions;

public interface IPermissionChecker
{
    bool IsSupported { get; }
    bool RequiresStartupPermissionGate { get; }
    bool IsAccessibilityTrusted();
    bool CheckUInputAccess();
    void OpenAccessibilitySettings();
}
