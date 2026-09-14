
namespace CrossMacro.Platform.Abstractions.WindowManagement;

/// <summary>
/// Composite interface for platforms that support all window operations.
/// </summary>
public interface IWindowManager : IWindowQueryService, IWindowMutationService, IWorkspaceManagementService
{
    /// <summary>Returns whether the current platform/session can perform window operations.</summary>
    public bool IsSupported => true;
}
