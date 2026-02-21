using Avalonia;
using CrossMacro.Platform.Windows.DependencyInjection;

namespace CrossMacro.UI.Windows;

internal static class Program
{
    [System.STAThread]
    public static int Main(string[] args)
    {
        return CrossMacro.UI.Program.Run(
            args,
            new WindowsPlatformServiceRegistrar(),
            static (appBuilder, startupArgs) => appBuilder.UseWin32().UseSkia().StartWithClassicDesktopLifetime(startupArgs));
    }
}
