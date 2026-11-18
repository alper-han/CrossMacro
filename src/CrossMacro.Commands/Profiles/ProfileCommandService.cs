
namespace CrossMacro.Commands.Profiles;

/// <summary>
/// Maps shared profile-operation outcomes to command results.
/// </summary>
public sealed class ProfileCommandService(IProfileOperations operations) : IProfileCommandService
{
    private readonly IProfileOperations _operations = operations ?? throw new ArgumentNullException(nameof(operations));

    public Task<OperationResult> ListAsync(CancellationToken cancellationToken) =>
        ExecuteAsync(new ProfileOperationRequest(ProfileOperationKind.List), cancellationToken);

    public Task<OperationResult> CurrentAsync(CancellationToken cancellationToken) =>
        ExecuteAsync(new ProfileOperationRequest(ProfileOperationKind.Current), cancellationToken);

    public Task<OperationResult> CreateAsync(string name, CancellationToken cancellationToken) =>
        ExecuteAsync(new ProfileOperationRequest(ProfileOperationKind.Create, DisplayName: name), cancellationToken);

    public Task<OperationResult> SwitchAsync(string profileIdentifier, CancellationToken cancellationToken) =>
        ExecuteAsync(new ProfileOperationRequest(ProfileOperationKind.Switch, Identifier: profileIdentifier), cancellationToken);

    public Task<OperationResult> RenameAsync(string profileIdentifier, string newName, CancellationToken cancellationToken) =>
        ExecuteAsync(new ProfileOperationRequest(ProfileOperationKind.Rename, profileIdentifier, newName), cancellationToken);

    public Task<OperationResult> DeleteAsync(string profileIdentifier, bool force, CancellationToken cancellationToken) =>
        ExecuteAsync(new ProfileOperationRequest(ProfileOperationKind.Delete, Identifier: profileIdentifier, Force: force), cancellationToken);

    private async Task<OperationResult> ExecuteAsync(ProfileOperationRequest request, CancellationToken cancellationToken)
    {
        var result = await _operations.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
        return result.Success
            ? ToSuccessResult(request.Operation, result)
            : ToFailureResult(result);
    }

    private static OperationResult ToSuccessResult(ProfileOperationKind operation, ProfileOperationResult result)
    {
        var snapshot = result.Snapshot ?? throw new InvalidOperationException("A successful profile operation returned no snapshot.");
        if (operation is ProfileOperationKind.List)
        {
            var data = new ProfileListData(
                snapshot.Profiles.Select(profile => ToData(profile, string.Equals(profile.Id, snapshot.ActiveProfileId, StringComparison.OrdinalIgnoreCase))).ToList(),
                snapshot.ActiveProfileId);
            return OperationResult.Ok(result.Message, data);
        }

        var profile = result.AffectedProfile ?? throw new InvalidOperationException("A successful profile operation returned no affected profile.");
        var isActive = GetIsActive(operation, profile, snapshot);
        return OperationResult.Ok(result.Message, ToData(profile, isActive));
    }

    private static OperationResult ToFailureResult(ProfileOperationResult result)
    {
        if (result.Failure is ProfileOperationFailureKind.ForceRequired)
        {
            return OperationResult.Fail(ExecutionOutcomeCode.InvalidArguments, "profile delete requires --force.", ["Pass --force to confirm profile deletion."]);
        }

        var exitCode = result.Failure is ProfileOperationFailureKind.SwitchFailed
            ? ExecutionOutcomeCode.RuntimeError
            : ExecutionOutcomeCode.InvalidArguments;
        IReadOnlyList<string> errors = string.IsNullOrWhiteSpace(result.ErrorDetail) ? [] : [result.ErrorDetail];
        return OperationResult.Fail(exitCode, result.Message, errors);
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
