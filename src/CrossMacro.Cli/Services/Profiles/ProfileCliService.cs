using CrossMacro.Application.Profiles;
namespace CrossMacro.Cli.Services.Profiles;

/// <summary>
/// Formats shared profile-operation outcomes for the CLI transport.
/// </summary>
public sealed class ProfileCliService : IProfileCliService
{
    private readonly IProfileOperations _operations;

    internal ProfileCliService(IProfileOperations operations)
    {
        _operations = operations ?? throw new ArgumentNullException(nameof(operations));
    }

    public ProfileCliService(IManageProfile manageProfile)
        : this(new ProfileOperations(manageProfile))
    {
    }

    public ProfileCliService(IProfileManager profileManager)
        : this(new ProfileOperations(new ManageProfile(profileManager)))
    {
    }

    public ProfileCliService(IManageProfile manageProfile, IProfileManager profileManager)
        : this(manageProfile)
    {
        ArgumentNullException.ThrowIfNull(profileManager);
    }

    public Task<CliCommandExecutionResult> ListAsync(CancellationToken cancellationToken) =>
        ExecuteAsync(new ProfileOperationRequest(ProfileOperationKind.List), cancellationToken);

    public Task<CliCommandExecutionResult> CurrentAsync(CancellationToken cancellationToken) =>
        ExecuteAsync(new ProfileOperationRequest(ProfileOperationKind.Current), cancellationToken);

    public Task<CliCommandExecutionResult> CreateAsync(string name, CancellationToken cancellationToken) =>
        ExecuteAsync(new ProfileOperationRequest(ProfileOperationKind.Create, DisplayName: name), cancellationToken);

    public Task<CliCommandExecutionResult> SwitchAsync(string profileIdentifier, CancellationToken cancellationToken) =>
        ExecuteAsync(new ProfileOperationRequest(ProfileOperationKind.Switch, Identifier: profileIdentifier), cancellationToken);

    public Task<CliCommandExecutionResult> RenameAsync(string profileIdentifier, string newName, CancellationToken cancellationToken) =>
        ExecuteAsync(new ProfileOperationRequest(ProfileOperationKind.Rename, profileIdentifier, newName), cancellationToken);

    public Task<CliCommandExecutionResult> DeleteAsync(string profileIdentifier, bool force, CancellationToken cancellationToken) =>
        ExecuteAsync(new ProfileOperationRequest(ProfileOperationKind.Delete, Identifier: profileIdentifier, Force: force), cancellationToken);

    private async Task<CliCommandExecutionResult> ExecuteAsync(ProfileOperationRequest request, CancellationToken cancellationToken)
    {
        var result = await _operations.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
        return result.Success
            ? ToSuccessResult(request.Operation, result)
            : ToFailureResult(result);
    }

    private static CliCommandExecutionResult ToSuccessResult(ProfileOperationKind operation, ProfileOperationResult result)
    {
        var snapshot = result.Snapshot ?? throw new InvalidOperationException("A successful profile operation returned no snapshot.");
        if (operation is ProfileOperationKind.List)
        {
            var data = new ProfileListData(
                snapshot.Profiles.Select(profile => ToData(profile, string.Equals(profile.Id, snapshot.ActiveProfileId, StringComparison.OrdinalIgnoreCase))).ToList(),
                snapshot.ActiveProfileId);
            return CliCommandExecutionResult.Ok(result.Message, data);
        }

        var profile = result.AffectedProfile ?? throw new InvalidOperationException("A successful profile operation returned no affected profile.");
        var isActive = GetIsActive(operation, profile, snapshot);
        return CliCommandExecutionResult.Ok(result.Message, ToData(profile, isActive));
    }

    private static CliCommandExecutionResult ToFailureResult(ProfileOperationResult result)
    {
        var exitCode = result.Failure is ProfileOperationFailureKind.SwitchFailed
            ? CliExitCode.RuntimeError
            : CliExitCode.InvalidArguments;
        IReadOnlyList<string> errors = string.IsNullOrWhiteSpace(result.ErrorDetail) ? [] : [result.ErrorDetail];
        return CliCommandExecutionResult.Fail(exitCode, result.Message, errors);
    }

    private static ProfileData ToData(ProfileInfo profile, bool isActive) =>
        new(profile.Id, profile.Name, profile.CreatedAt, isActive);

    private static bool GetIsActive(ProfileOperationKind operation, ProfileInfo profile, ProfileResult snapshot)
    {
        if (operation is ProfileOperationKind.Create)
        {
            return true;
        }

        if (operation is ProfileOperationKind.Delete)
        {
            return false;
        }

        if (operation is ProfileOperationKind.Current or ProfileOperationKind.Switch or ProfileOperationKind.Rename)
        {
            return string.Equals(profile.Id, snapshot.ActiveProfileId, StringComparison.OrdinalIgnoreCase);
        }

        throw new ArgumentOutOfRangeException(nameof(operation), operation, "Unexpected profile operation.");
    }
}
