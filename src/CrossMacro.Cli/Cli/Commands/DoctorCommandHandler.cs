using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Cli.Services;

namespace CrossMacro.Cli.Commands;

public sealed class DoctorCommandHandler : CliCommandHandlerBase<DoctorCliOptions>
{
    private readonly IDoctorService _doctorService;

    public DoctorCommandHandler(IDoctorService doctorService)
    {
        _doctorService = doctorService;
    }

    protected override async Task<CliCommandExecutionResult> ExecuteAsync(DoctorCliOptions options, CancellationToken cancellationToken)
    {
        var report = await _doctorService.RunAsync(options.Verbose, cancellationToken);

        var warningMessages = report.Checks
            .Where(x => x.Status == DoctorCheckStatus.Warn)
            .Select(x => $"{x.Name}: {x.Message}")
            .ToArray();

        var errorMessages = report.Checks
            .Where(x => x.Status == DoctorCheckStatus.Fail)
            .Select(x => $"{x.Name}: {x.Message}")
            .ToArray();

        var data = new
        {
            checks = report.Checks.Select(x => new
            {
                name = x.Name,
                status = x.Status.ToString().ToLowerInvariant(),
                message = x.Message,
                details = x.Details
            }).ToArray()
        };

        if (report.HasFailures)
        {
            return CliCommandExecutionResult.Fail(
                CliExitCode.EnvironmentError,
                "Doctor checks found blocking issues.",
                errors: errorMessages,
                warnings: warningMessages,
                data: data);
        }

        var message = report.HasWarnings
            ? "Doctor checks completed with warnings."
            : "Doctor checks passed.";

        return CliCommandExecutionResult.Ok(message, data, warningMessages);
    }
}
