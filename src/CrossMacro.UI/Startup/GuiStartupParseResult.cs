namespace CrossMacro.UI.Startup;

public sealed record GuiStartupParseResult(
    GuiStartupOptions Options,
    IReadOnlyList<string> ForwardedArgs);
