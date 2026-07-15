using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Cli.Services;

namespace CrossMacro.Cli.Commands;

public sealed class ClipboardCommandHandler : CliCommandHandlerBase<ClipboardCliOptions>
{
    private readonly IClipboardCliService _clipboardCliService;

    public ClipboardCommandHandler(IClipboardCliService clipboardCliService)
    {
        _clipboardCliService = clipboardCliService;
    }

    protected override Task<CliCommandExecutionResult> ExecuteAsync(ClipboardCliOptions options, CancellationToken cancellationToken)
    {
        return options.Action switch
        {
            ClipboardCliAction.Get => _clipboardCliService.GetAsync(cancellationToken),
            ClipboardCliAction.Set when options.FilePath is not null => _clipboardCliService.SetFileAsync(options.FilePath, cancellationToken),
            ClipboardCliAction.Set => _clipboardCliService.SetTextAsync(options.Text ?? string.Empty, cancellationToken),
            ClipboardCliAction.Clear => _clipboardCliService.ClearAsync(cancellationToken),
            _ => Task.FromResult(CliCommandExecutionResult.Fail(CliExitCode.InvalidArguments, "Unknown clipboard action.")),
        };
    }
}
