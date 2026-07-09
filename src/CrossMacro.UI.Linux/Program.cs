using System;
using Avalonia;
using CrossMacro.Cli;
using CrossMacro.Platform.Linux.DependencyInjection;
using Serilog;
using System.Threading.Tasks;

namespace CrossMacro.UI.Linux;

internal static class Program
{
    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => CrossMacro.UI.Program.BuildAvaloniaApp()
            .UseLinuxWindowingSubsystem()
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
                    .UseLinuxWindowingSubsystem()
                    .UseSkia()),
            getVersionString: CrossMacro.UI.Program.GetVersionString,
            tryAcquireSingleInstanceGuard: CrossMacro.UI.Program.TryAcquireRuntimeSingleInstanceGuard);
    }

    private static AppBuilder UseLinuxWindowingSubsystem(this AppBuilder builder)
    {
        builder.UseStandardRuntimePlatformSubsystem();

        builder.UseWindowingSubsystem(() =>
        {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
            {
                try
                {
                    builder.UseWayland();
                    builder.WindowingSubsystemInitializer?.Invoke();
                    return;
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Wayland initialization failed, falling back to X11");
                }
            }

            builder.UseX11();
            builder.WindowingSubsystemInitializer?.Invoke();
        }, "Wayland/X11 Fallback");

        return builder;
    }
}
