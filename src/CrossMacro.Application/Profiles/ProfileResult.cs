
namespace CrossMacro.Application.Profiles;

public sealed record ProfileResult(
    ProfileInfo? Profile,
    IReadOnlyList<ProfileInfo> Profiles,
    string ActiveProfileId);
