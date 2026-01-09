using Avalonia;
using System;
using Serilog;
using CrossMacro.Core.Logging;
using CrossMacro.Infrastructure.Services;

namespace CrossMacro.UI;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Load log level from settings before logger initialization
        var logLevel = SettingsService.TryLoadLogLevelEarly();
        
        // Initialize logger with user's preferred level
        LoggerSetup.Initialize(logLevel);

        try
        {
            Log.Information("Starting CrossMacro application");
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
