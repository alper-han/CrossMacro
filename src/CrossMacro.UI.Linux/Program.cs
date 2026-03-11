using Avalonia;
using CrossMacro.Cli;
using CrossMacro.Platform.Linux.DependencyInjection;

namespace CrossMacro.UI.Linux;

internal static class Program
{
    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => CrossMacro.UI.Program.BuildAvaloniaApp()
            .UseX11()
            .UseSkia();

    [System.STAThread]
    public static int Main(string[] args)
    {
        var platformServiceRegistrar = new LinuxPlatformServiceRegistrar();

        return CliGuiRuntime.Run(
            args,
            platformServiceRegistrar,
            startGui: () => CrossMacro.UI.Program.RunGui(
                args,
                platformServiceRegistrar,
                static (appBuilder, startupArgs) => appBuilder
                    .UseX11()
                    .UseSkia()
                    .StartWithClassicDesktopLifetime(startupArgs)),
            getVersionString: CrossMacro.UI.Program.GetVersionString,
            tryAcquireSingleInstanceGuard: CrossMacro.UI.Program.TryAcquireRuntimeSingleInstanceGuard);
    }
}
