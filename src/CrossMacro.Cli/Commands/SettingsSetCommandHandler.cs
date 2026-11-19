
namespace CrossMacro.Cli.Commands;

public sealed class SettingsSetCommandHandler(ISettingsCommandService settingsCommandService) : CliCommandHandlerBase<SettingsSetOptions>
{
    private readonly ISettingsCommandService _settingsCommandService = settingsCommandService ?? throw new ArgumentNullException(nameof(settingsCommandService));

    protected override Task<OperationResult> ExecuteAsync(SettingsSetOptions options, CancellationToken cancellationToken) =>
        _settingsCommandService.SetAsync(options.Key, options.Value, cancellationToken);
}
