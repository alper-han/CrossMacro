using Avalonia;
using CrossMacro.Cli;
using CrossMacro.Platform.Windows.DependencyInjection;
using System.Threading.Tasks;

namespace CrossMacro.UI.Windows;

internal static class Program
{
    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => CrossMacro.UI.Program.BuildAvaloniaApp()
            .UseWin32()
            .UseSkia();

    [System.STAThread]
    public static Task<int> Main(string[] args)
    {
        var platformServiceRegistrar = new WindowsPlatformServiceRegistrar();

        return CliGuiRuntime.RunAsync(
            args,
            platformServiceRegistrar,
            startGui: () => CrossMacro.UI.Program.RunGui(
                args,
                platformServiceRegistrar,
                static appBuilder => appBuilder.UseWin32().UseSkia()),
            getVersionString: CrossMacro.UI.Program.GetVersionString,
            tryAcquireSingleInstanceGuard: CrossMacro.UI.Program.TryAcquireRuntimeSingleInstanceGuard);
    }
}
