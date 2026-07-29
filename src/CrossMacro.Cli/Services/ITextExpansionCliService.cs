
namespace CrossMacro.Cli.Services;

public interface ITextExpansionCliService
{
    public Task<CliCommandExecutionResult> ListAsync(string? profileIdentifier, CancellationToken cancellationToken);

    public Task<CliCommandExecutionResult> AddAsync(
        string trigger,
        string replacement,
        PasteMethod method,
        TextInsertionMode insertionMode,
        DirectTypingMethod directTypingMethod,
        string? profileIdentifier,
        CancellationToken cancellationToken);

    public Task<CliCommandExecutionResult> RemoveAsync(string trigger, string? profileIdentifier, CancellationToken cancellationToken);

    public Task<CliCommandExecutionResult> EnableAsync(string trigger, string? profileIdentifier, CancellationToken cancellationToken);

    public Task<CliCommandExecutionResult> DisableAsync(string trigger, string? profileIdentifier, CancellationToken cancellationToken);

    public Task<CliCommandExecutionResult> TestAsync(string trigger, string? profileIdentifier, CancellationToken cancellationToken);
}
