
namespace CrossMacro.Commands.Profiles;

public sealed record ProfileListData(IReadOnlyList<ProfileData> Profiles, string ActiveProfileId);
