
namespace CrossMacro.Cli.Services;

public interface IDoctorService
{
    public Task<DoctorReport> RunAsync(bool verbose, CancellationToken cancellationToken);
}
