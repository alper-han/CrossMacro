using System;
using System.Collections.Generic;

namespace CrossMacro.UI.Startup;

public static class GuiStartupOptionsParser
{
    public static GuiStartupParseResult Parse(string[]? args)
    {
        if (args == null || args.Length == 0)
        {
            return new GuiStartupParseResult(GuiStartupOptions.Default, []);
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
            [.. forwardedArgs]);
    }

    private static bool IsStartMinimizedToken(string arg)
    {
        return string.Equals(arg, "--start-minimized", StringComparison.OrdinalIgnoreCase);
    }
}
