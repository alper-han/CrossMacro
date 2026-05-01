using Avalonia;
using CrossMacro.Cli;
using CrossMacro.Platform.MacOS.DependencyInjection;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace CrossMacro.UI.MacOS;

[SupportedOSPlatform("macos")]
internal static class Program
{
    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => CrossMacro.UI.Program.BuildAvaloniaApp()
            .UseAvaloniaNative()
            .UseSkia();

    [System.STAThread]
    public static Task<int> Main(string[] args)
    {
        var platformServiceRegistrar = new MacOSPlatformServiceRegistrar();

        return CliGuiRuntime.RunAsync(
            args,
            platformServiceRegistrar,
            startGui: () => CrossMacro.UI.Program.RunGui(
                args,
                platformServiceRegistrar,
                static appBuilder => appBuilder.UseAvaloniaNative().UseSkia()),
            getVersionString: CrossMacro.UI.Program.GetVersionString,
            tryAcquireSingleInstanceGuard: CrossMacro.UI.Program.TryAcquireRuntimeSingleInstanceGuard);
    }
}
