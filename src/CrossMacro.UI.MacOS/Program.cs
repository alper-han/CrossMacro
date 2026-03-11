using Avalonia;
using CrossMacro.Cli;
using CrossMacro.Platform.MacOS.DependencyInjection;
using System.Runtime.Versioning;

namespace CrossMacro.UI.MacOS;

[SupportedOSPlatform("macos")]
internal static class Program
{
    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => CrossMacro.UI.Program.BuildAvaloniaApp()
            .UseAvaloniaNative();

    [System.STAThread]
    public static int Main(string[] args)
    {
        var platformServiceRegistrar = new MacOSPlatformServiceRegistrar();

        return CliGuiRuntime.Run(
            args,
            platformServiceRegistrar,
            startGui: () => CrossMacro.UI.Program.RunGui(
                args,
                platformServiceRegistrar,
                static (appBuilder, startupArgs) => appBuilder.UseAvaloniaNative().StartWithClassicDesktopLifetime(startupArgs)),
            getVersionString: CrossMacro.UI.Program.GetVersionString,
            tryAcquireSingleInstanceGuard: CrossMacro.UI.Program.TryAcquireRuntimeSingleInstanceGuard);
    }
}
