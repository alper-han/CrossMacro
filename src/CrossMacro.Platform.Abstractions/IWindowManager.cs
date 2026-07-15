
namespace CrossMacro.Platform.Abstractions;

/// <summary>
/// Composite interface for platforms that support all window operations.
/// </summary>
public interface IWindowManager : IWindowQueryService, IWindowMutationService, IWorkspaceManagementService
{
    /// <summary>Returns whether the current platform/session can perform window operations.</summary>
    public bool IsSupported => true;
}
