using System.Threading;
using System.Threading.Tasks;

namespace CrossMacro.Cli.Services;

public interface IDoctorService
{
    Task<DoctorReport> RunAsync(bool verbose, CancellationToken cancellationToken);
}
