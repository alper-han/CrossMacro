
namespace CrossMacro.Platform.Abstractions.Setup;

public interface IFlatpakQuickSetupService
{
    public bool IsApplicable();

    public Task<QuickSetupResult> RunAsync(CancellationToken cancellationToken = default);
}
