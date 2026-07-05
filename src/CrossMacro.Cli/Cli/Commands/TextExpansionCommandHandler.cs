using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Cli.Services;

namespace CrossMacro.Cli.Commands;

public sealed class TextExpansionCommandHandler : CliCommandHandlerBase<TextExpansionCliOptions>
{
    private readonly ITextExpansionCliService _textExpansionCliService;

    public TextExpansionCommandHandler(ITextExpansionCliService textExpansionCliService)
    {
        _textExpansionCliService = textExpansionCliService;
    }

    protected override Task<CliCommandExecutionResult> ExecuteAsync(TextExpansionCliOptions options, CancellationToken cancellationToken)
    {
        return options.Action switch
        {
            TextExpansionCliAction.List => _textExpansionCliService.ListAsync(options.ProfileIdentifier, cancellationToken),
            TextExpansionCliAction.Add => _textExpansionCliService.AddAsync(
                options.Trigger ?? string.Empty,
                options.Replacement ?? string.Empty,
                options.Method,
                options.InsertionMode,
                options.DirectTypingMethod,
                options.ProfileIdentifier,
                cancellationToken),
            TextExpansionCliAction.Remove => _textExpansionCliService.RemoveAsync(options.Trigger ?? string.Empty, options.ProfileIdentifier, cancellationToken),
            TextExpansionCliAction.Enable => _textExpansionCliService.EnableAsync(options.Trigger ?? string.Empty, options.ProfileIdentifier, cancellationToken),
            TextExpansionCliAction.Disable => _textExpansionCliService.DisableAsync(options.Trigger ?? string.Empty, options.ProfileIdentifier, cancellationToken),
            TextExpansionCliAction.Test => _textExpansionCliService.TestAsync(options.Trigger ?? string.Empty, options.ProfileIdentifier, cancellationToken),
            _ => Task.FromResult(CliCommandExecutionResult.Fail(CliExitCode.InvalidArguments, "Unknown text-expansion action."))
        };
    }
}
