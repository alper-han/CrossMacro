
namespace CrossMacro.Cli.Services;

public interface IProfileCliService
{
    public Task<CliCommandExecutionResult> ListAsync(CancellationToken cancellationToken);

    public Task<CliCommandExecutionResult> CurrentAsync(CancellationToken cancellationToken);

    public Task<CliCommandExecutionResult> CreateAsync(string name, CancellationToken cancellationToken);

    public Task<CliCommandExecutionResult> SwitchAsync(string profileIdentifier, CancellationToken cancellationToken);

    public Task<CliCommandExecutionResult> RenameAsync(string profileIdentifier, string newName, CancellationToken cancellationToken);

    public Task<CliCommandExecutionResult> DeleteAsync(string profileIdentifier, bool force, CancellationToken cancellationToken);
}
