using System.Threading;
using System.Threading.Tasks;

namespace CrossMacro.Infrastructure.Services.ScreenReading;

public interface IScreenReadingWarmupService
{
    Task WarmUpPortalSessionAsync(CancellationToken cancellationToken = default);
}
