
namespace CrossMacro.Platform.Abstractions.Setup;

public interface IAppImageQuickSetupService
{
    public bool IsApplicable();
    public bool ShouldPrompt();
    public Task<QuickSetupResult> RunAsync(CancellationToken cancellationToken = default);
}
