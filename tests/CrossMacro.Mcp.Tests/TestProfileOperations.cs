namespace CrossMacro.Mcp.Tests;

internal sealed class TestProfileOperations : IProfileOperations
{
    public ProfileOperationResult? Result { get; init; }

    public int CallCount { get; private set; }

    public ProfileOperationRequest? LastRequest { get; private set; }

    public Task<ProfileOperationResult> ExecuteAsync(ProfileOperationRequest request, CancellationToken cancellationToken = default)
    {
        CallCount++;
        LastRequest = request;
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Result ?? new ProfileOperationResult(
            new ProfileResult(
                Profile: null,
                Profiles: [],
                ActiveProfileId: string.Empty),
            AffectedProfile: null,
            Message: "0 profile(s)."));
    }
}
