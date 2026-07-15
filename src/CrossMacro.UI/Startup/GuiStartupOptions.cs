namespace CrossMacro.UI.Startup;

public sealed record class GuiStartupOptions(bool StartMinimized = false)
{
    public static GuiStartupOptions Default { get; } = new();
}
