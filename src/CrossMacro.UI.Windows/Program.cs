using Avalonia;
using CrossMacro.Cli;
using CrossMacro.Platform.Windows.DependencyInjection;

namespace CrossMacro.UI.Windows;

internal static class Program
{
    [System.STAThread]
    public static int Main(string[] args)
    {
        var platformServiceRegistrar = new WindowsPlatformServiceRegistrar();

        return CliGuiRuntime.Run(
            args,
            platformServiceRegistrar,
            startGui: () => CrossMacro.UI.Program.RunGui(
                args,
                platformServiceRegistrar,
                static (appBuilder, startupArgs) => appBuilder.UseWin32().UseSkia().StartWithClassicDesktopLifetime(startupArgs)),
            getVersionString: CrossMacro.UI.Program.GetVersionString,
            tryAcquireSingleInstanceGuard: CrossMacro.UI.Program.TryAcquireRuntimeSingleInstanceGuard);
    }
}
