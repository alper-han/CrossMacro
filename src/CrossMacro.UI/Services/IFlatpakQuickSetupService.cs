using System.Threading;
using System.Threading.Tasks;

namespace CrossMacro.UI.Services;

public readonly record struct FlatpakQuickSetupResult(bool Success, string Message);

public interface IFlatpakQuickSetupService
{
    bool IsApplicable();

    Task<FlatpakQuickSetupResult> RunAsync(CancellationToken cancellationToken = default);
}
