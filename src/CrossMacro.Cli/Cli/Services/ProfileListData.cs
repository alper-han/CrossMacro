
namespace CrossMacro.Cli.Services;

public sealed record class ProfileListData(IReadOnlyList<ProfileData> Profiles, string ActiveProfileId);
