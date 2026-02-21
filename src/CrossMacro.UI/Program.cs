using Avalonia;
using Avalonia.Media;
using CrossMacro.Core.Logging;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Services;
using Serilog;
using System;

namespace CrossMacro.UI;

public static class Program
{
    private const string SingleInstanceName = "CrossMacro.UI.SingleInstance";

    public static int Run(
        string[] args,
        IPlatformServiceRegistrar platformServiceRegistrar,
        Action<AppBuilder, string[]> startApplication)
    {
        ArgumentNullException.ThrowIfNull(platformServiceRegistrar);
        ArgumentNullException.ThrowIfNull(startApplication);

        // Load log level from settings before logger initialization
        var logLevel = SettingsService.TryLoadLogLevelEarly();
        
        // Initialize logger with user's preferred level
        LoggerSetup.Initialize(logLevel);

        SingleInstanceGuard? instanceGuard = null;

        try
        {
            instanceGuard = SingleInstanceGuard.TryAcquire(GetSingleInstanceMutexName());
            if (instanceGuard == null)
            {
                Log.Warning("Could not acquire single-instance lock; another instance may already be running.");
                return 0;
            }

            App.PlatformServiceRegistrar = platformServiceRegistrar;
            Log.Information("Starting CrossMacro application");
            startApplication(BuildAvaloniaApp(), args);
            return 0;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
            return 1;
        }
        finally
        {
            instanceGuard?.Dispose();
            App.PlatformServiceRegistrar = null;
            Log.CloseAndFlush();
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
}
