
namespace CrossMacro.Cli.Services.Diagnostics;

public interface IDoctorService
{
    public Task<DoctorReport> RunAsync(bool verbose, CancellationToken cancellationToken);
}
