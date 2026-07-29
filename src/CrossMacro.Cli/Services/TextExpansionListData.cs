
namespace CrossMacro.Cli.Services;

public sealed record TextExpansionListData(IReadOnlyList<TextExpansionData> Expansions, string ProfileId, int Count);
