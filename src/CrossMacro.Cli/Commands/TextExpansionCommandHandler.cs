using CrossMacro.Commands.Automation;

namespace CrossMacro.Cli.Commands;

public sealed class TextExpansionCommandHandler(ITextExpansionCommandService textExpansionCommandService) : CliCommandHandlerBase<TextExpansionOptions>
{
    private readonly ITextExpansionCommandService _textExpansionCommandService = textExpansionCommandService ?? throw new ArgumentNullException(nameof(textExpansionCommandService));

    protected override Task<OperationResult> ExecuteAsync(TextExpansionOptions options, CancellationToken cancellationToken)
    {
        return options.Action switch
        {
            TextExpansionAction.List => _textExpansionCommandService.ListAsync(options.ProfileIdentifier, cancellationToken),
            TextExpansionAction.Add => _textExpansionCommandService.AddAsync(
                options.Trigger ?? string.Empty,
                options.Replacement ?? string.Empty,
                options.Method,
                options.InsertionMode,
                options.DirectTypingMethod,
                options.ProfileIdentifier,
                cancellationToken),
            TextExpansionAction.Remove => _textExpansionCommandService.RemoveAsync(options.Trigger ?? string.Empty, options.ProfileIdentifier, cancellationToken),
            TextExpansionAction.Enable => _textExpansionCommandService.EnableAsync(options.Trigger ?? string.Empty, options.ProfileIdentifier, cancellationToken),
            TextExpansionAction.Disable => _textExpansionCommandService.DisableAsync(options.Trigger ?? string.Empty, options.ProfileIdentifier, cancellationToken),
            TextExpansionAction.Test => _textExpansionCommandService.TestAsync(options.Trigger ?? string.Empty, options.ProfileIdentifier, cancellationToken),
            _ => Task.FromResult(OperationResult.Fail(ExecutionOutcomeCode.InvalidArguments, "Unknown text-expansion action.")),
        };
    }
}
