using System.Threading;
using System.Threading.Tasks;

namespace CrossMacro.Core.Services;

public interface IAppImageQuickSetupService
{
    bool IsApplicable();
    bool ShouldPrompt();
    Task<QuickSetupResult> RunAsync(CancellationToken cancellationToken = default);
}
