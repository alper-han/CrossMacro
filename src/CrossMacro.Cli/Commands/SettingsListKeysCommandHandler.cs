
namespace CrossMacro.Cli.Commands;

public sealed class SettingsListKeysCommandHandler(ISettingsCliService settingsCliService) : CliCommandHandlerBase<SettingsListKeysCliOptions>
{
    private readonly ISettingsCliService _settingsCliService = settingsCliService;

    protected override async Task<CliCommandExecutionResult> ExecuteAsync(SettingsListKeysCliOptions options, CancellationToken cancellationToken)
    {
        var result = await _settingsCliService.ListKeysAsync(cancellationToken).ConfigureAwait(false);
        return result.Success
            ? CliCommandExecutionResult.Ok(result.Message, result.Data)
            : CliCommandExecutionResult.Fail(result.ExitCode, result.Message, errors: result.Errors);
    }
}
