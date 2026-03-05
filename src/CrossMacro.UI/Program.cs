using Avalonia;
using Avalonia.Media;
using CrossMacro.Core.Services;
using Serilog;
using System;
using System.Reflection;

namespace CrossMacro.UI;

public static class Program
{
    private const string SingleInstanceName = "CrossMacro.UI.SingleInstance";

    public static int RunGui(
        string[] args,
        IPlatformServiceRegistrar platformServiceRegistrar,
        Action<AppBuilder, string[]> startApplication)
    {
        ArgumentNullException.ThrowIfNull(platformServiceRegistrar);
        ArgumentNullException.ThrowIfNull(startApplication);

        try
        {
            App.PlatformServiceRegistrar = platformServiceRegistrar;
            Log.Information("Starting CrossMacro application");
            startApplication(BuildAvaloniaApp(), args);
            return 0;
        }
        finally
        {
            App.PlatformServiceRegistrar = null;
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .WithInterFont()
            .LogToTrace()
            .With(new FontManagerOptions
            {
                DefaultFamilyName = "avares://Avalonia.Fonts.Inter/Assets#Inter",
                FontFallbacks =
                [
                    new FontFallback { FontFamily = new FontFamily("avares://Avalonia.Fonts.Inter/Assets#Inter") }
                ]
            });

    private static string GetSingleInstanceMutexName()
    {
        // Windows without "Global\" is session scoped; we want system-wide single instance.
        return OperatingSystem.IsWindows() ? $@"Global\{SingleInstanceName}" : SingleInstanceName;
    }

    public static IDisposable? TryAcquireRuntimeSingleInstanceGuard()
    {
        return SingleInstanceGuard.TryAcquire(GetSingleInstanceMutexName());
    }

    public static string GetVersionString()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var name = assembly.GetName();
        var version = name.Version;
        if (version == null)
        {
            return name.Name ?? "CrossMacro";
        }

        return $"{name.Name} {version.Major}.{version.Minor}.{version.Build}";
    }
}
