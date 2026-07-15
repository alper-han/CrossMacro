
namespace CrossMacro.Packaging.Abstractions;

public interface IAppImageQuickSetupService
{
    public bool IsApplicable();
    public bool ShouldPrompt();
    public Task<QuickSetupResult> RunAsync(CancellationToken cancellationToken = default);
}
