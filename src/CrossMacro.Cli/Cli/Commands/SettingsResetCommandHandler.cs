
namespace CrossMacro.Cli.Commands;

public sealed class SettingsResetCommandHandler : CliCommandHandlerBase<SettingsResetCliOptions>
{
    private readonly ISettingsCliService _settingsCliService;

    public SettingsResetCommandHandler(ISettingsCliService settingsCliService)
    {
        _settingsCliService = settingsCliService;
    }

    protected override async Task<CliCommandExecutionResult> ExecuteAsync(SettingsResetCliOptions options, CancellationToken cancellationToken)
    {
        var result = await _settingsCliService.ResetAsync(options.Key, cancellationToken);
        return result.Success
            ? CliCommandExecutionResult.Ok(result.Message, result.Data)
            : CliCommandExecutionResult.Fail(result.ExitCode, result.Message, errors: result.Errors);
    }
}
