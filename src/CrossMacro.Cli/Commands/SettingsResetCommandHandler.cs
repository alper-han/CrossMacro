
namespace CrossMacro.Cli.Commands;

public sealed class SettingsResetCommandHandler(ISettingsCommandService settingsCommandService) : CliCommandHandlerBase<SettingsResetOptions>
{
    private readonly ISettingsCommandService _settingsCommandService = settingsCommandService ?? throw new ArgumentNullException(nameof(settingsCommandService));

    protected override Task<OperationResult> ExecuteAsync(SettingsResetOptions options, CancellationToken cancellationToken) =>
        _settingsCommandService.ResetAsync(options.Key, cancellationToken);
}
