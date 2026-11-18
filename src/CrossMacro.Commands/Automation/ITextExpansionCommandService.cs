
namespace CrossMacro.Commands.Automation;

public interface ITextExpansionCommandService
{
    public Task<OperationResult> ListAsync(string? profileIdentifier, CancellationToken cancellationToken);

    public Task<OperationResult> AddAsync(
        string trigger,
        string replacement,
        PasteMethod method,
        TextInsertionMode insertionMode,
        DirectTypingMethod directTypingMethod,
        string? profileIdentifier,
        CancellationToken cancellationToken);

    public Task<OperationResult> RemoveAsync(string trigger, string? profileIdentifier, CancellationToken cancellationToken);

    public Task<OperationResult> EnableAsync(string trigger, string? profileIdentifier, CancellationToken cancellationToken);

    public Task<OperationResult> DisableAsync(string trigger, string? profileIdentifier, CancellationToken cancellationToken);

    public Task<OperationResult> TestAsync(string trigger, string? profileIdentifier, CancellationToken cancellationToken);
}
