
namespace CrossMacro.Cli.Serialization;

public sealed record MacroEventBreakdownData(
    [property: JsonPropertyName("mouseMove")] int MouseMove,
    [property: JsonPropertyName("buttonPress")] int ButtonPress,
    [property: JsonPropertyName("buttonRelease")] int ButtonRelease,
    [property: JsonPropertyName("click")] int Click,
    [property: JsonPropertyName("keyPress")] int KeyPress,
    [property: JsonPropertyName("keyRelease")] int KeyRelease
);
