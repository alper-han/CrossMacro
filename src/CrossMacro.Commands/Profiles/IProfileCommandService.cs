
namespace CrossMacro.Commands.Profiles;

public interface IProfileCommandService
{
    public Task<OperationResult> ListAsync(CancellationToken cancellationToken);

    public Task<OperationResult> CurrentAsync(CancellationToken cancellationToken);

    public Task<OperationResult> CreateAsync(string name, CancellationToken cancellationToken);

    public Task<OperationResult> SwitchAsync(string profileIdentifier, CancellationToken cancellationToken);

    public Task<OperationResult> RenameAsync(string profileIdentifier, string newName, CancellationToken cancellationToken);

    public Task<OperationResult> DeleteAsync(string profileIdentifier, bool force, CancellationToken cancellationToken);
}
