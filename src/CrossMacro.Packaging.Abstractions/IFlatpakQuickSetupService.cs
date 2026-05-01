using System.Threading;
using System.Threading.Tasks;

namespace CrossMacro.Packaging.Abstractions;

public interface IFlatpakQuickSetupService
{
    bool IsApplicable();

    Task<QuickSetupResult> RunAsync(CancellationToken cancellationToken = default);
}
