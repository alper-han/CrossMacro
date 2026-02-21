using Avalonia;
using CrossMacro.Platform.MacOS.DependencyInjection;
using System.Runtime.Versioning;

namespace CrossMacro.UI.MacOS;

[SupportedOSPlatform("macos")]
internal static class Program
{
    [System.STAThread]
    public static int Main(string[] args)
    {
        return CrossMacro.UI.Program.Run(
            args,
            new MacOSPlatformServiceRegistrar(),
            static (appBuilder, startupArgs) => appBuilder.UseAvaloniaNative().StartWithClassicDesktopLifetime(startupArgs));
    }
}
