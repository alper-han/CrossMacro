
namespace CrossMacro.Cli.Serialization;

public sealed record MacroEventBreakdownData(
    int MouseMove,
    int ButtonPress,
    int ButtonRelease,
    int Click,
    int KeyPress,
    int KeyRelease
);
