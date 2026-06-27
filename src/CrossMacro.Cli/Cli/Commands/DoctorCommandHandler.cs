using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Cli.Services;
using CrossMacro.Cli.Serialization;

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

        var data = new DoctorCommandData(
            report.Checks.Select(x => new DoctorCheckOutput(
                x.Name,
                x.Status.ToString().ToLowerInvariant(),
                x.Message,
                x.Details
            )).ToArray()
        );

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
