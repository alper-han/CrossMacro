using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Cli.Services;

namespace CrossMacro.Cli.Commands;

public sealed class SettingsSetCommandHandler : CliCommandHandlerBase<SettingsSetCliOptions>
{
    private readonly ISettingsCliService _settingsCliService;

    public SettingsSetCommandHandler(ISettingsCliService settingsCliService)
    {
        _settingsCliService = settingsCliService;
    }

    protected override async Task<CliCommandExecutionResult> ExecuteAsync(SettingsSetCliOptions options, CancellationToken cancellationToken)
    {
        var result = await _settingsCliService.SetAsync(options.Key, options.Value, cancellationToken);

        return result.Success
            ? CliCommandExecutionResult.Ok(result.Message, result.Data)
            : CliCommandExecutionResult.Fail(result.ExitCode, result.Message, errors: result.Errors);
    }
}
