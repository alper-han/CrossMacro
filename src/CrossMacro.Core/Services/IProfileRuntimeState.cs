namespace CrossMacro.Core.Services;

/// <summary>
/// Exposes whether profile-backed runtime data is ready for consumers that
/// should only start their services after the initial profile load.
/// </summary>
public interface IProfileRuntimeState
{
    public bool IsInitialized { get; }
}
