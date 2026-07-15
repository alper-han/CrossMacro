
namespace CrossMacro.Cli.Services;

public interface IProfileCliService
{
    Task<CliCommandExecutionResult> ListAsync(CancellationToken cancellationToken);

    Task<CliCommandExecutionResult> CurrentAsync(CancellationToken cancellationToken);

    Task<CliCommandExecutionResult> CreateAsync(string name, CancellationToken cancellationToken);

    Task<CliCommandExecutionResult> SwitchAsync(string profileIdentifier, CancellationToken cancellationToken);

    Task<CliCommandExecutionResult> RenameAsync(string profileIdentifier, string newName, CancellationToken cancellationToken);

    Task<CliCommandExecutionResult> DeleteAsync(string profileIdentifier, bool force, CancellationToken cancellationToken);
}
