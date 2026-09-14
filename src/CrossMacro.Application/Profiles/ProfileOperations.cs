namespace CrossMacro.Application.Profiles;

/// <summary>
/// Centralizes profile operation semantics that are shared by CLI and MCP adapters.
/// </summary>
public sealed class ProfileOperations(IManageProfile profiles) : IProfileOperations
{
    private readonly IManageProfile _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));

    public Task<ProfileOperationResult> ExecuteAsync(ProfileOperationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        return request.Operation switch
        {
            ProfileOperationKind.List => ListAsync(cancellationToken),
            ProfileOperationKind.Current => CurrentAsync(cancellationToken),
            ProfileOperationKind.Create => CreateAsync(request.DisplayName ?? string.Empty, cancellationToken),
            ProfileOperationKind.Switch => SwitchAsync(request.Identifier ?? string.Empty, cancellationToken),
            ProfileOperationKind.Rename => RenameAsync(request.Identifier ?? string.Empty, request.DisplayName ?? string.Empty, cancellationToken),
            ProfileOperationKind.Delete => DeleteAsync(request.Identifier ?? string.Empty, request.Force, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(request), request.Operation, "Unknown profile operation."),
        };
    }

    private async Task<ProfileOperationResult> ListAsync(CancellationToken cancellationToken)
    {
        var snapshot = await _profiles.ListAsync(cancellationToken).ConfigureAwait(false);
        return Success(snapshot, affectedProfile: null, $"{snapshot.Profiles.Count} profile(s).");
    }

    private async Task<ProfileOperationResult> CurrentAsync(CancellationToken cancellationToken)
    {
        var snapshot = await _profiles.CurrentAsync(cancellationToken).ConfigureAwait(false);
        var profile = snapshot.Profile ?? throw new InvalidOperationException("The current profile operation returned no profile.");
        return Success(snapshot, profile, $"Current profile: {profile.Name} ({profile.Id}).");
    }

    private async Task<ProfileOperationResult> CreateAsync(string name, CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await _profiles.CreateAsync(new ProfileRequest(DisplayName: name), cancellationToken).ConfigureAwait(false);
            var profile = snapshot.Profile ?? throw new InvalidOperationException("The create profile operation returned no profile.");
            return Success(snapshot, profile, $"Profile created: {profile.Name} ({profile.Id}).");
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not OperationCanceledException)
        {
            return Failure(ProfileOperationFailureKind.CreateFailed, "Failed to create profile.", exception.Message);
        }
    }

    private async Task<ProfileOperationResult> SwitchAsync(string identifier, CancellationToken cancellationToken)
    {
        var resolved = await ResolveAsync(identifier, cancellationToken).ConfigureAwait(false);
        if (resolved.Failure is not null)
        {
            return resolved.Failure;
        }

        var profile = resolved.Profile!;
        try
        {
            var snapshot = await _profiles.SwitchAsync(new ProfileRequest(Identifier: profile.Id), cancellationToken).ConfigureAwait(false);
            return Success(snapshot, profile, $"Switched to profile: {profile.Name} ({profile.Id}).");
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not OperationCanceledException)
        {
            return Failure(ProfileOperationFailureKind.SwitchFailed, "Failed to switch profile.", exception.Message);
        }
    }

    private async Task<ProfileOperationResult> RenameAsync(string identifier, string newName, CancellationToken cancellationToken)
    {
        var resolved = await ResolveAsync(identifier, cancellationToken).ConfigureAwait(false);
        if (resolved.Failure is not null)
        {
            return resolved.Failure;
        }

        try
        {
            var snapshot = await _profiles.RenameAsync(new ProfileRequest(resolved.Profile!.Id, newName), cancellationToken).ConfigureAwait(false);
            var profile = snapshot.Profile ?? throw new InvalidOperationException("The rename profile operation returned no profile.");
            return Success(snapshot, profile, $"Profile renamed: {profile.Name} ({profile.Id}).");
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not OperationCanceledException)
        {
            return Failure(ProfileOperationFailureKind.RenameFailed, "Failed to rename profile.", exception.Message);
        }
    }

    private async Task<ProfileOperationResult> DeleteAsync(string identifier, bool force, CancellationToken cancellationToken)
    {
        if (!force)
        {
            return Failure(
                ProfileOperationFailureKind.ForceRequired,
                "profile delete requires --force.",
                "Pass --force to confirm profile deletion.");
        }

        var resolved = await ResolveAsync(identifier, cancellationToken).ConfigureAwait(false);
        if (resolved.Failure is not null)
        {
            return resolved.Failure;
        }

        var profile = resolved.Profile!;
        try
        {
            var snapshot = await _profiles.DeleteAsync(new ProfileRequest(Identifier: profile.Id), cancellationToken).ConfigureAwait(false);
            return Success(snapshot, profile, $"Profile deleted: {profile.Name} ({profile.Id}).");
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not OperationCanceledException)
        {
            return Failure(ProfileOperationFailureKind.DeleteFailed, "Failed to delete profile.", exception.Message);
        }
    }

    private async Task<(ProfileInfo? Profile, ProfileOperationResult? Failure)> ResolveAsync(string identifier, CancellationToken cancellationToken)
    {
        var snapshot = await _profiles.ListAsync(cancellationToken).ConfigureAwait(false);
        var profile = snapshot.Profiles.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, identifier, StringComparison.OrdinalIgnoreCase)
            || string.Equals(candidate.Name, identifier, StringComparison.OrdinalIgnoreCase));

        return profile is null
            ? (null, Failure(ProfileOperationFailureKind.ProfileNotFound, "Profile not found.", $"Unknown profile: {identifier}"))
            : (profile, null);
    }

    private static ProfileOperationResult Success(ProfileResult snapshot, ProfileInfo? affectedProfile, string message) =>
        new(snapshot, affectedProfile, message);

    private static ProfileOperationResult Failure(ProfileOperationFailureKind failure, string message, string? detail) =>
        new(
            Snapshot: null,
            AffectedProfile: null,
            Message: message,
            Failure: failure,
            ErrorDetail: detail);
}
