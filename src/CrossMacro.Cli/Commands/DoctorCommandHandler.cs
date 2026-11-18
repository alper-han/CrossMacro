
namespace CrossMacro.Cli.Commands;

public sealed class DoctorCommandHandler(IDoctorService doctorService) : CliCommandHandlerBase<DoctorOptions>
{
    private readonly IDoctorService _doctorService = doctorService;

    protected override async Task<OperationResult> ExecuteAsync(DoctorOptions options, CancellationToken cancellationToken)
    {
        var report = await _doctorService.RunAsync(options.Verbose, cancellationToken).ConfigureAwait(false);

        var warningMessages = report.Checks
            .Where(x => x.Status is DoctorCheckStatus.Warn)
            .Select(x => $"{x.Name}: {x.Message}")
            .ToArray();

        var errorMessages = report.Checks
            .Where(x => x.Status is DoctorCheckStatus.Fail)
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
            return OperationResult.Fail(
                ExecutionOutcomeCode.EnvironmentError,
                "Doctor checks found blocking issues.",
                errors: errorMessages,
                warnings: warningMessages,
                data: data);
        }

        var message = report.HasWarnings
            ? "Doctor checks completed with warnings."
            : "Doctor checks passed.";

        return OperationResult.Ok(message, data, warningMessages);
    }
}
