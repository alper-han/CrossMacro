
namespace CrossMacro.Cli.Commands;

public sealed class SettingsGetCommandHandler(ISettingsCommandService settingsCommandService) : CliCommandHandlerBase<SettingsGetOptions>
{
    private readonly ISettingsCommandService _settingsCommandService = settingsCommandService ?? throw new ArgumentNullException(nameof(settingsCommandService));

    protected override async Task<OperationResult> ExecuteAsync(SettingsGetOptions options, CancellationToken cancellationToken)
    {
        var result = await _settingsCommandService.GetAsync(options.Key, cancellationToken).ConfigureAwait(false);

        if (result.Success && !options.JsonOutput && (options.Key is null || options.All) && result.Data is Dictionary<string, object?> allSettings)
        {
            var lines = allSettings
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .Select(x => $"{x.Key}={x.Value}")
                .ToArray();

            var message = lines.Length is 0
                ? "No settings available."
                : string.Join('\n', lines);

            return OperationResult.Ok(message, result.Data);
        }

        return result;
    }
}
