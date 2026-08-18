namespace CrossMacro.UI.Startup;

public sealed record GuiStartupParseResult(
    GuiStartupOptions Options,
    IReadOnlyList<string> ForwardedArgs)
{
    public GuiStartupParseResult(GuiStartupOptions options, string[] forwardedArgs)
        : this(options, (IReadOnlyList<string>)forwardedArgs)
    {
    }

    public GuiStartupParseResult(GuiStartupOptions options, ReadOnlySpan<string> forwardedArgs)
        : this(options, forwardedArgs.ToArray())
    {
    }
}
