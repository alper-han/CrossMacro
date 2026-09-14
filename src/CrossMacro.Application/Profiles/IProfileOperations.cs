namespace CrossMacro.Application.Profiles;

/// <summary>
/// Executes profile use cases and exposes a typed, transport-independent outcome.
/// </summary>
public interface IProfileOperations
{
    public Task<ProfileOperationResult> ExecuteAsync(ProfileOperationRequest request, CancellationToken cancellationToken = default);
}
