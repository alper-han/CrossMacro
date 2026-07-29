
namespace CrossMacro.UI.Startup;

public static class GuiStartupOptionsParser
{
    public static GuiStartupParseResult Parse(string[]? args)
    {
        return args is null
            ? new GuiStartupParseResult(GuiStartupOptions.Default, (IReadOnlyList<string>)[])
            : Parse(args.AsSpan());
    }

    public static GuiStartupParseResult Parse(ReadOnlySpan<string> args)
    {
        if (args.Length is 0)
        {
            return new GuiStartupParseResult(GuiStartupOptions.Default, (IReadOnlyList<string>)[]);
        }

        var forwardedArgs = new List<string>(args.Length);
        var startMinimized = false;

        foreach (var arg in args)
        {
            if (IsStartMinimizedToken(arg))
            {
                startMinimized = true;
                continue;
            }

            forwardedArgs.Add(arg);
        }

        return new GuiStartupParseResult(
            new GuiStartupOptions(StartMinimized: startMinimized),
            forwardedArgs.ToArray());
    }

    private static bool IsStartMinimizedToken(string arg)
    {
        return string.Equals(arg, "--start-minimized", StringComparison.OrdinalIgnoreCase);
    }
}
