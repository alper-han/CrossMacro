namespace CrossMacro.UI.Startup;

public sealed record GuiStartupOptions(bool StartMinimized = false)
{
    public static GuiStartupOptions Default { get; } = new();
}

public sealed record GuiStartupParseResult(GuiStartupOptions Options, string[] ForwardedArgs);
