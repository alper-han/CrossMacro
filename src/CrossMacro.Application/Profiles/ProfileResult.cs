
namespace CrossMacro.Application.Profiles;

public sealed record class ProfileResult(
    ProfileInfo? Profile,
    IReadOnlyList<ProfileInfo> Profiles,
    string ActiveProfileId);
