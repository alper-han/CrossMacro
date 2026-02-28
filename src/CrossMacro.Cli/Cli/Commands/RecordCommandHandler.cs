using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Cli.Services;

namespace CrossMacro.Cli.Commands;

public sealed class RecordCommandHandler : CliCommandHandlerBase<RecordCliOptions>
{
    private readonly IRecordExecutionService _recordExecutionService;
    private readonly ICliPreflightService _cliPreflightService;

    public RecordCommandHandler(IRecordExecutionService recordExecutionService, ICliPreflightService cliPreflightService)
    {
        _recordExecutionService = recordExecutionService;
        _cliPreflightService = cliPreflightService;
    }

    protected override async Task<CliCommandExecutionResult> ExecuteAsync(RecordCliOptions options, CancellationToken cancellationToken)
    {
        var preflight = await _cliPreflightService.CheckAsync(CliPreflightTarget.Record, cancellationToken);
        if (!preflight.Success)
        {
            return CliCommandExecutionResult.Fail(preflight.ExitCode, preflight.Message, preflight.Errors, preflight.Warnings);
        }

        var result = await _recordExecutionService.ExecuteAsync(new RecordExecutionRequest
        {
            OutputFilePath = options.OutputFilePath,
            RecordMouse = options.RecordMouse,
            RecordKeyboard = options.RecordKeyboard,
            CoordinateMode = options.CoordinateMode,
            SkipInitialZero = options.SkipInitialZero,
            DurationSeconds = options.DurationSeconds
        }, cancellationToken);

        return result.Success
            ? CliCommandExecutionResult.Ok(result.Message, result.Data, result.Warnings)
            : CliCommandExecutionResult.Fail(result.ExitCode, result.Message, result.Errors, result.Warnings, result.Data);
    }
}
