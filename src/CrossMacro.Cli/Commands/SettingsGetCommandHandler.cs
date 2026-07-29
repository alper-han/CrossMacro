
namespace CrossMacro.Cli.Commands;

public sealed class SettingsGetCommandHandler(ISettingsCliService settingsCliService) : CliCommandHandlerBase<SettingsGetCliOptions>
{
    private readonly ISettingsCliService _settingsCliService = settingsCliService;

    protected override async Task<CliCommandExecutionResult> ExecuteAsync(SettingsGetCliOptions options, CancellationToken cancellationToken)
    {
        var result = await _settingsCliService.GetAsync(options.Key, cancellationToken).ConfigureAwait(false);

        if (result.Success && !options.JsonOutput && (options.Key is null || options.All) && result.Data is Dictionary<string, object?> allSettings)
        {
            var lines = allSettings
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .Select(x => $"{x.Key}={x.Value}")
                .ToArray();

            var message = lines.Length is 0
                ? "No settings available."
                : string.Join('\n', lines);

            return CliCommandExecutionResult.Ok(message, result.Data);
        }

        return result.Success
            ? CliCommandExecutionResult.Ok(result.Message, result.Data)
            : CliCommandExecutionResult.Fail(result.ExitCode, result.Message, errors: result.Errors);
    }
}
