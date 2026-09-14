
namespace CrossMacro.Cli.Services.Profiles;

public sealed record ProfileListData(IReadOnlyList<ProfileData> Profiles, string ActiveProfileId);
