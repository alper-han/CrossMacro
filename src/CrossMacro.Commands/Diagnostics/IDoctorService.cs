
namespace CrossMacro.Commands.Diagnostics;

public interface IDoctorService
{
    public Task<DoctorReport> RunAsync(bool verbose, CancellationToken cancellationToken);
}
