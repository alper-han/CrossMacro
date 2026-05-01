using Avalonia;
using CrossMacro.Cli;
using CrossMacro.Platform.Linux.DependencyInjection;
using System.Threading.Tasks;

namespace CrossMacro.UI.Linux;

internal static class Program
{
    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => CrossMacro.UI.Program.BuildAvaloniaApp()
            .UseX11()
            .UseSkia();

    [System.STAThread]
    public static Task<int> Main(string[] args)
    {
        var platformServiceRegistrar = new LinuxPlatformServiceRegistrar();

        return CliGuiRuntime.RunAsync(
            args,
            platformServiceRegistrar,
            startGui: () => CrossMacro.UI.Program.RunGui(
                args,
                platformServiceRegistrar,
                static appBuilder => appBuilder
                    .UseX11()
                    .UseSkia()),
            getVersionString: CrossMacro.UI.Program.GetVersionString,
            tryAcquireSingleInstanceGuard: CrossMacro.UI.Program.TryAcquireRuntimeSingleInstanceGuard);
    }
}
