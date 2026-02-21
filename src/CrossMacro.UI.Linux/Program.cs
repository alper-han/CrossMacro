using Avalonia;
using CrossMacro.Platform.Linux.DependencyInjection;

namespace CrossMacro.UI.Linux;

internal static class Program
{
    [System.STAThread]
    public static int Main(string[] args)
    {
        return CrossMacro.UI.Program.Run(
            args,
            new LinuxPlatformServiceRegistrar(),
            static (appBuilder, startupArgs) => appBuilder
                .UseX11()
                .UseSkia()
                .StartWithClassicDesktopLifetime(startupArgs));
    }
}
