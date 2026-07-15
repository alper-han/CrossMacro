
namespace CrossMacro.Cli.Services;

public sealed record class WindowListData(IReadOnlyList<WindowInfoData> Windows, int Count);
