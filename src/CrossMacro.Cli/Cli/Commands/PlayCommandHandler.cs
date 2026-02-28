using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Cli.Services;

namespace CrossMacro.Cli.Commands;

public sealed class PlayCommandHandler : CliCommandHandlerBase<PlayCliOptions>
{
    private readonly IMacroExecutionService _macroExecutionService;
    private readonly ICliPreflightService _cliPreflightService;

    public PlayCommandHandler(IMacroExecutionService macroExecutionService, ICliPreflightService cliPreflightService)
    {
        _macroExecutionService = macroExecutionService;
        _cliPreflightService = cliPreflightService;
    }

    protected override async Task<CliCommandExecutionResult> ExecuteAsync(PlayCliOptions options, CancellationToken cancellationToken)
    {
        if (!options.DryRun)
        {
            var preflight = await _cliPreflightService.CheckAsync(CliPreflightTarget.Play, cancellationToken);
            if (!preflight.Success)
            {
                return CliCommandExecutionResult.Fail(preflight.ExitCode, preflight.Message, preflight.Errors, preflight.Warnings);
            }
        }

        return await CommandTimeoutRunner.RunAsync(
            options.TimeoutSeconds,
            cancellationToken,
            token => ExecuteInternalAsync(options, token));
    }

    private async Task<CliCommandExecutionResult> ExecuteInternalAsync(PlayCliOptions options, CancellationToken cancellationToken)
    {
        var effectiveLoop = options.Loop || options.RepeatCount != 1;

        var request = new MacroExecutionRequest
        {
            MacroFilePath = options.MacroFilePath,
            SpeedMultiplier = options.SpeedMultiplier,
            Loop = effectiveLoop,
            RepeatCount = options.RepeatCount,
            RepeatDelayMs = options.RepeatDelayMs,
            CountdownSeconds = options.CountdownSeconds,
            DryRun = options.DryRun
        };

        var result = await _macroExecutionService.ExecuteAsync(request, cancellationToken);

        return result.Success
            ? CliCommandExecutionResult.Ok(result.Message, result.Data, result.Warnings)
            : CliCommandExecutionResult.Fail(result.ExitCode, result.Message, result.Errors, result.Warnings, result.Data);
    }
}
