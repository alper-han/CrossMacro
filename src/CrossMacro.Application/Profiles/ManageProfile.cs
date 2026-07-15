
namespace CrossMacro.Application.Profiles;

public sealed class ManageProfile : IManageProfile
{
    private readonly IProfileManager _profiles;

    public ManageProfile(IProfileManager profiles)
    {
        _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
    }

    public Task<ProfileResult> ListAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(CreateResult(profile: null));
    }

    public Task<ProfileResult> CurrentAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(CreateResult(_profiles.ActiveProfile));
    }

    public async Task<ProfileResult> CreateAsync(ProfileRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var profile = await _profiles.CreateProfileAsync(request.DisplayName ?? string.Empty).ConfigureAwait(false);
        return CreateResult(profile);
    }

    public async Task<ProfileResult> SwitchAsync(ProfileRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var profile = Resolve(request.Identifier);
        await _profiles.SwitchProfileAsync(profile.Id).ConfigureAwait(false);
        return CreateResult(profile);
    }

    public async Task<ProfileResult> RenameAsync(ProfileRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var profile = Resolve(request.Identifier);
        await _profiles.RenameProfileAsync(profile.Id, request.DisplayName ?? string.Empty).ConfigureAwait(false);
        var renamed = _profiles.Profiles.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, profile.Id, StringComparison.OrdinalIgnoreCase))
            ?? new ProfileInfo { Id = profile.Id, Name = request.DisplayName ?? string.Empty, CreatedAt = profile.CreatedAt };
        return CreateResult(renamed);
    }

    public async Task<ProfileResult> DeleteAsync(ProfileRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var profile = Resolve(request.Identifier);
        await _profiles.DeleteProfileAsync(profile.Id).ConfigureAwait(false);
        return CreateResult(profile);
    }

    private ProfileInfo Resolve(string? identifier)
    {
        var profile = _profiles.Profiles.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, identifier, StringComparison.OrdinalIgnoreCase)
            || string.Equals(candidate.Name, identifier, StringComparison.OrdinalIgnoreCase));
        return profile ?? throw new ArgumentException($"Unknown profile: {identifier}", nameof(identifier));
    }

    private ProfileResult CreateResult(ProfileInfo? profile) =>
        new(profile, _profiles.Profiles.ToArray(), _profiles.ActiveProfile.Id);
}
