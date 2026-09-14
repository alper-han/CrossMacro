
namespace CrossMacro.Cli.Services.Automation;

public sealed record TextExpansionListData(IReadOnlyList<TextExpansionData> Expansions, string ProfileId, int Count);
