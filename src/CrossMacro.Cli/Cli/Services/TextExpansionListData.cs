
namespace CrossMacro.Cli.Services;

public sealed record class TextExpansionListData(IReadOnlyList<TextExpansionData> Expansions, string ProfileId, int Count);
