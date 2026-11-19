using CrossMacro.Commands.Clipboard;

namespace CrossMacro.Cli.Commands;

public sealed class ClipboardCommandHandler(IClipboardCommandService clipboardCommandService) : CliCommandHandlerBase<ClipboardOptions>
{
    private readonly IClipboardCommandService _clipboardCommandService = clipboardCommandService;

    protected override Task<OperationResult> ExecuteAsync(ClipboardOptions options, CancellationToken cancellationToken)
    {
        return options.Action switch
        {
            ClipboardAction.Get => _clipboardCommandService.GetAsync(cancellationToken),
            ClipboardAction.Set when options.FilePath is not null => _clipboardCommandService.SetFileAsync(options.FilePath, cancellationToken),
            ClipboardAction.Set => _clipboardCommandService.SetTextAsync(options.Text ?? string.Empty, cancellationToken),
            ClipboardAction.Clear => _clipboardCommandService.ClearAsync(cancellationToken),
            _ => Task.FromResult(OperationResult.Fail(ExecutionOutcomeCode.InvalidArguments, "Unknown clipboard action.")),
        };
    }
}
