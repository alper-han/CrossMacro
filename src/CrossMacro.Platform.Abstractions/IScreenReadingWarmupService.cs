using System.Threading;
using System.Threading.Tasks;

namespace CrossMacro.Platform.Abstractions;

public interface IScreenReadingWarmupService
{
    Task WarmUpPortalSessionAsync(CancellationToken cancellationToken = default);
}
