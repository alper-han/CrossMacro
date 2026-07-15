
namespace CrossMacro.Packaging.Abstractions;

public interface IFlatpakQuickSetupService
{
    public bool IsApplicable();

    public Task<QuickSetupResult> RunAsync(CancellationToken cancellationToken = default);
}
