using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Threading;
using CrossMacro.Core.Services;
using CrossMacro.UI.Localization;
using CrossMacro.UI.Services;
using CrossMacro.UI.Startup;
using CrossMacro.UI.Views.Tabs;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace CrossMacro.UI;

public static class Program
{
    private const string SingleInstanceName = "CrossMacro.UI.SingleInstance";

    public static int RunGui(
        string[] args,
        IPlatformServiceRegistrar platformServiceRegistrar,
        Func<AppBuilder, AppBuilder> configureAppBuilder)
    {
        ArgumentNullException.ThrowIfNull(platformServiceRegistrar);
        ArgumentNullException.ThrowIfNull(configureAppBuilder);

        var startupParseResult = GuiStartupOptionsParser.Parse(args);

        try
        {
            App.PlatformServiceRegistrar = platformServiceRegistrar;
            App.StartupOptions = startupParseResult.Options;
            Log.Information("Starting CrossMacro application");

            return configureAppBuilder(BuildAvaloniaApp())
                .StartWithClassicDesktopLifetime(
                    startupParseResult.ForwardedArgs,
                    ConfigureDesktopStartup);
        }
        finally
        {
            App.PlatformServiceRegistrar = null;
            App.StartupOptions = GuiStartupOptions.Default;
        }
    }

    private static void ConfigureDesktopStartup(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var desktopLifetime = desktop as ClassicDesktopStyleApplicationLifetime
            ?? throw new InvalidOperationException("Unexpected desktop lifetime type.");

        // Let Avalonia complete lifetime wiring via StartWithClassicDesktopLifetime(), then queue the
        // app-specific startup while MainWindow is still null so tray-first startup doesn't flash a window.
        desktopLifetime.Startup += (_, _) => QueueDesktopStartup(desktopLifetime);
    }

    private static void QueueDesktopStartup(IClassicDesktopStyleApplicationLifetime desktop)
    {
        try
        {
            var startupCoordinator = GetDesktopStartupCoordinator();
            Dispatcher.UIThread.Post(
                () => _ = RunStartupAsync(startupCoordinator, desktop),
                DispatcherPriority.Send);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Desktop startup initialization failed");
            Dispatcher.UIThread.Post(() => desktop.Shutdown(1), DispatcherPriority.Send);
        }
    }

    private static IDesktopStartupCoordinator GetDesktopStartupCoordinator()
    {
        var application = Application.Current as App
            ?? throw new InvalidOperationException("Avalonia application is not initialized.");

        var services = application.Services
            ?? throw new InvalidOperationException("Service provider is not initialized.");

        InitializeLocalization(services);

        return services.GetRequiredService<IDesktopStartupCoordinator>();
    }

    private static void InitializeLocalization(IServiceProvider services)
    {
        var settingsService = services.GetRequiredService<ISettingsService>();
        settingsService.Load();

        var localizationService = services.GetRequiredService<LocalizationService>();
        LocalizationBindingSource.Instance.Initialize(localizationService);
        localizationService.SetCulture(settingsService.Current.Language);
        ActionTypeConverters.Configure(services.GetRequiredService<EditorActionDisplayFormatter>());
        ScheduleTaskConverters.Configure(localizationService);
    }

    private static async Task RunStartupAsync(
        IDesktopStartupCoordinator startupCoordinator,
        IClassicDesktopStyleApplicationLifetime desktop)
    {
        try
        {
            await startupCoordinator.StartAsync(desktop);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Desktop startup failed");
            desktop.Shutdown(1);
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .WithInterFont()
            .UseHarfBuzz()
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
