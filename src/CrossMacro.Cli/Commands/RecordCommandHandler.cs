
namespace CrossMacro.Cli.Commands;

public sealed class RecordCommandHandler(IRecordExecutionService recordExecutionService, IPreflightService preflightService) : CliCommandHandlerBase<RecordOptions>
{
    private readonly IRecordExecutionService _recordExecutionService = recordExecutionService;
    private readonly IPreflightService _preflightService = preflightService;

    protected override async Task<OperationResult> ExecuteAsync(RecordOptions options, CancellationToken cancellationToken)
    {
        var preflight = await _preflightService.CheckAsync(PreflightTarget.Record, cancellationToken).ConfigureAwait(false);
        if (!preflight.Success)
        {
            return preflight;
        }

        return await _recordExecutionService.ExecuteAsync(new RecordExecutionRequest
        {
            OutputFilePath = options.OutputFilePath,
            RecordMouse = options.RecordMouse,
            RecordKeyboard = options.RecordKeyboard,
            CoordinateMode = options.CoordinateMode,
            SkipInitialZero = options.SkipInitialZero,
            DurationSeconds = options.DurationSeconds,
        }, cancellationToken).ConfigureAwait(false);
    }
}
