using System;
using Microsoft.Extensions.DependencyInjection;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Core.Services.Recording.Processors;
using CrossMacro.Core.Services.Recording.Strategies;
using CrossMacro.Infrastructure.Services;
using CrossMacro.Core.Services.TextExpansion;
using CrossMacro.Infrastructure.Services.TextExpansion;
using CrossMacro.UI.ViewModels;
using CrossMacro.UI.Services;

namespace CrossMacro.UI.DependencyInjection;

/// <summary>
/// Extension methods for configuring CrossMacro services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Main entry point - registers all services for the application.
    /// </summary>
    public static IServiceCollection AddCrossMacroServices(
        this IServiceCollection services,
        IPlatformServiceRegistrar platformServiceRegistrar)
    {
        services.AddCrossMacroGuiRuntimeServices(platformServiceRegistrar);
        services.AddViewModels();
        
        return services;
    }

    /// <summary>
    /// Backward-compatible alias for GUI runtime service registration.
    /// </summary>
    public static IServiceCollection AddCrossMacroRuntimeServices(
        this IServiceCollection services,
        IPlatformServiceRegistrar platformServiceRegistrar)
    {
        return services.AddCrossMacroGuiRuntimeServices(platformServiceRegistrar);
    }

    /// <summary>
    /// Registers runtime services for GUI execution.
    /// </summary>
    public static IServiceCollection AddCrossMacroGuiRuntimeServices(
        this IServiceCollection services,
        IPlatformServiceRegistrar platformServiceRegistrar)
    {
        ArgumentNullException.ThrowIfNull(platformServiceRegistrar);

        services.AddCommonServices();
        platformServiceRegistrar.RegisterPlatformServices(services);
        services.AddSharedPostPlatformServices(includeGuiOnlyServices: true, allowAvaloniaClipboardFallback: true);
        
        return services;
    }

    /// <summary>
    /// Registers platform-agnostic core services.
    /// </summary>
    public static IServiceCollection AddCommonServices(this IServiceCollection services)
    {
        services.AddSingleton<IRuntimeContext, RuntimeContext>();
        services.AddSingleton<IHotkeyConfigurationService, HotkeyConfigurationService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<HotkeySettings>(sp => 
            sp.GetRequiredService<IHotkeyConfigurationService>().Load());
        services.AddSingleton<ITimeProvider, SystemTimeProvider>();
        services.AddSingleton<IMacroFileManager, MacroFileManager>();
        
        // Recording processor factory
        services.AddSingleton<Func<ICoordinateStrategy, IInputEventProcessor>>(sp => 
            strategy => new StandardInputEventProcessor(strategy));
        
        // MacroRecorder with optional inputSimulatorFactory for corner reset
        services.AddTransient<IMacroRecorder>(sp =>
        {
            var captureFactory = sp.GetService<Func<IInputCapture>>();
            var strategyFactory = sp.GetRequiredService<ICoordinateStrategyFactory>();
            var processorFactory = sp.GetRequiredService<Func<ICoordinateStrategy, IInputEventProcessor>>();
            var simulatorFactory = sp.GetService<Func<IInputSimulator>>();
            
            return new MacroRecorder(
                captureFactory,
                strategyFactory,
                processorFactory,
                simulatorFactory);
        });
        
        return services;
    }

    /// <summary>
    /// Registers services that depend on platform services being registered first.
    /// </summary>
    public static IServiceCollection AddSharedPostPlatformServices(
        this IServiceCollection services,
        bool includeGuiOnlyServices = true,
        bool allowAvaloniaClipboardFallback = true)
    {
        // GlobalHotkeyService depends on Func<IInputCapture>
        // Register Core Services for GlobalHotkeyService
        services.AddSingleton<IKeyCodeMapper, KeyCodeMapper>();
        services.AddSingleton<IMouseButtonMapper, MouseButtonMapper>();
        services.AddSingleton<IModifierStateTracker, ModifierStateTracker>();
        services.AddSingleton<IHotkeyParser, HotkeyParser>();
        services.AddSingleton<IHotkeyStringBuilder, HotkeyStringBuilder>();
        services.AddSingleton<IHotkeyMatcher, HotkeyMatcher>();

        // GlobalHotkeyService depends on Func<IInputCapture>
        services.AddSingleton<IGlobalHotkeyService>(sp =>
        {
            var configService = sp.GetRequiredService<IHotkeyConfigurationService>();
            var hotkeyParser = sp.GetRequiredService<IHotkeyParser>();
            var hotkeyMatcher = sp.GetRequiredService<IHotkeyMatcher>();
            var modifierTracker = sp.GetRequiredService<IModifierStateTracker>();
            var hotkeyStringBuilder = sp.GetRequiredService<IHotkeyStringBuilder>();
            var mouseButtonMapper = sp.GetRequiredService<IMouseButtonMapper>();
            var captureFactory = sp.GetService<Func<IInputCapture>>();
            
            return new GlobalHotkeyService(
                configService, 
                hotkeyParser, 
                hotkeyMatcher, 
                modifierTracker, 
                hotkeyStringBuilder, 
                mouseButtonMapper, 
                captureFactory);
        });

        services.AddTransient<PlaybackValidator>();

        // MacroPlayer with optional pool
        services.AddTransient<IMacroPlayer>(sp =>
        {
            var positionProvider = sp.GetRequiredService<IMousePositionProvider>();
            var validator = sp.GetRequiredService<PlaybackValidator>();
            var factory = sp.GetService<Func<IInputSimulator>>();
            var pool = sp.GetService<InputSimulatorPool>();
            var playbackBehaviorPolicy = sp.GetService<IPlaybackBehaviorPolicy>();
            return new MacroPlayer(positionProvider, validator, 
                inputSimulatorFactory: factory, simulatorPool: pool, playbackBehaviorPolicy: playbackBehaviorPolicy);
        });

        services.AddSingleton<Func<IMacroPlayer>>(sp => () => sp.GetRequiredService<IMacroPlayer>());

        RegisterClipboardServices(services, allowAvaloniaClipboardFallback);

        // Common services
        services.AddSingleton<IScheduledTaskRepository, JsonScheduledTaskRepository>();
        services.AddSingleton<IScheduledTaskExecutor, MacroScheduledTaskExecutor>();
        services.AddSingleton<ISchedulerService, SchedulerService>();
        services.AddSingleton<IShortcutService, ShortcutService>();
        services.AddSingleton<ITextExpansionStorageService, TextExpansionStorageService>();
        
        // Text Expansion Service Components
        services.AddTransient<IInputProcessor, InputProcessor>();
        services.AddTransient<ITextBufferState, TextBufferState>();
        services.AddTransient<ITextExpansionExecutor, TextExpansionExecutor>();
        services.AddSingleton<ITextExpansionService, TextExpansionService>();
        
        // Editor services
        services.AddSingleton<IEditorActionConverter, EditorActionConverter>();
        services.AddSingleton<IEditorActionValidator, EditorActionValidator>();
        services.AddSingleton<ICoordinateCaptureService>(sp =>
        {
            var positionProvider = sp.GetRequiredService<IMousePositionProvider>();
            var captureFactory = sp.GetService<Func<IInputCapture>>();
            return new CoordinateCaptureService(positionProvider, captureFactory);
        });

        if (includeGuiOnlyServices)
        {
            services.AddSingleton<ITrayIconService, TrayIconService>();
            services.AddSingleton<IDialogService, DialogService>();
            services.AddSingleton<IUpdateService, GitHubUpdateService>();
            services.AddSingleton<IExternalUrlOpener, ExternalUrlOpener>();
            services.AddSingleton<IThemeService, ThemeService>();
        }

        return services;
    }

    private static void RegisterClipboardServices(IServiceCollection services, bool allowAvaloniaClipboardFallback)
    {
        if (allowAvaloniaClipboardFallback)
        {
            services.AddSingleton<AvaloniaClipboardService>();
            if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
            {
                services.AddSingleton<IClipboardService>(sp => sp.GetRequiredService<AvaloniaClipboardService>());
            }
            else
            {
                services.AddSingleton<IProcessRunner, ProcessRunner>();
                services.AddSingleton<LinuxShellClipboardService>();
                services.AddSingleton<IClipboardService, CompositeClipboardService>();
            }

            return;
        }

        if (OperatingSystem.IsLinux())
        {
            services.AddSingleton<IProcessRunner, ProcessRunner>();
            services.AddSingleton<LinuxShellClipboardService>();
            services.AddSingleton<IClipboardService>(sp => sp.GetRequiredService<LinuxShellClipboardService>());
            return;
        }

        services.AddSingleton<IClipboardService, NoOpClipboardService>();
    }

    /// <summary>
    /// Registers all ViewModels.
    /// </summary>
    public static IServiceCollection AddViewModels(this IServiceCollection services)
    {
        services.AddSingleton<RecordingViewModel>();
        services.AddSingleton<PlaybackViewModel>();
        services.AddSingleton<FilesViewModel>();
        services.AddSingleton<TextExpansionViewModel>();
        services.AddSingleton<ScheduleViewModel>();
        services.AddSingleton<ShortcutViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<EditorViewModel>();
        services.AddSingleton<MainWindowViewModel>();
        
        return services;
    }

}
