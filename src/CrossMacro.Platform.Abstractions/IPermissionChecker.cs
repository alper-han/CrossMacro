namespace CrossMacro.Platform.Abstractions;

public interface IPermissionChecker
{
    public bool IsSupported { get; }
    public bool RequiresStartupPermissionGate { get; }
    public bool IsAccessibilityTrusted();
    public bool CheckUInputAccess();

    public ValueTask<bool> CheckUInputAccessAsync(CancellationToken cancellationToken = default);

    public void OpenAccessibilitySettings();
}
