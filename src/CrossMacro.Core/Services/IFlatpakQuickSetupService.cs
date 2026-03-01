using System.Threading;
using System.Threading.Tasks;

namespace CrossMacro.Core.Services;

public readonly record struct FlatpakQuickSetupResult(bool Success, string Message);

public interface IFlatpakQuickSetupService
{
    bool IsApplicable();

    Task<FlatpakQuickSetupResult> RunAsync(CancellationToken cancellationToken = default);
}
