using System.Collections.Generic;

namespace CrossMacro.Cli.Services;

public sealed record WindowListData(IReadOnlyList<WindowInfoData> Windows, int Count);
