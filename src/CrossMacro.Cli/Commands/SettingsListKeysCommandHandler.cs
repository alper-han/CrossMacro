
namespace CrossMacro.Cli.Commands;

public sealed class SettingsListKeysCommandHandler(ISettingsCommandService settingsCommandService) : CliCommandHandlerBase<SettingsListKeysOptions>
{
    private readonly ISettingsCommandService _settingsCommandService = settingsCommandService ?? throw new ArgumentNullException(nameof(settingsCommandService));

    protected override Task<OperationResult> ExecuteAsync(SettingsListKeysOptions options, CancellationToken cancellationToken) =>
        _settingsCommandService.ListKeysAsync(cancellationToken);
}
