using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Models;

namespace CrossMacro.Cli.Services;

public interface ITextExpansionCliService
{
    Task<CliCommandExecutionResult> ListAsync(string? profileIdentifier, CancellationToken cancellationToken);

    Task<CliCommandExecutionResult> AddAsync(
        string trigger,
        string replacement,
        PasteMethod method,
        TextInsertionMode insertionMode,
        DirectTypingMethod directTypingMethod,
        string? profileIdentifier,
        CancellationToken cancellationToken);

    Task<CliCommandExecutionResult> RemoveAsync(string trigger, string? profileIdentifier, CancellationToken cancellationToken);

    Task<CliCommandExecutionResult> EnableAsync(string trigger, string? profileIdentifier, CancellationToken cancellationToken);

    Task<CliCommandExecutionResult> DisableAsync(string trigger, string? profileIdentifier, CancellationToken cancellationToken);

    Task<CliCommandExecutionResult> TestAsync(string trigger, string? profileIdentifier, CancellationToken cancellationToken);
}
