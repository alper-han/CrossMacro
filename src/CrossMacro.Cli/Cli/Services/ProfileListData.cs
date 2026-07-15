using System.Collections.Generic;

namespace CrossMacro.Cli.Services;

public sealed record ProfileListData(IReadOnlyList<ProfileData> Profiles, string ActiveProfileId);
