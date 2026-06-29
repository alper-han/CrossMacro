using System;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Logging;
using CrossMacro.Infrastructure.Services;
using CrossMacro.Infrastructure.Services.ScreenReading;
using CrossMacro.Infrastructure.Services.Recording.Processors;
using CrossMacro.Infrastructure.Services.Recording.Strategies;
using CrossMacro.Infrastructure.Services.TextExpansion;
using CrossMacro.Core.Services.TextExpansion;
using CrossMacro.Platform.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrossMacro.Infrastructure.DependencyInjection;

/// <summary>
/// Shared runtime DI registrations consumed by both GUI and CLI hosts.
/// The two public entry points intentionally separate registrations that are safe
/// before platform wiring from shared runtime registrations that depend on
/// platform-provided seams being available.
/// </summary>
public static class RuntimeServiceCollectionExtensions
{
    /// <summary>
    /// Registers shared runtime services that do not require platform-specific
    /// implementations to be present yet.
    /// </summary>
    public static IServiceCollection AddCrossMacroCommonRuntimeServices(this IServiceCollection services)
    {
        RegisterPersistenceAndConfigurationServices(services);
        RegisterRuntimePrimitiveServices(services);
        RegisterRecordingServices(services);

        return services;
    }

    /// <summary>
    /// Registers shared runtime services that are composed after platform-specific
    /// services have supplied the required input, display, and simulation seams.
    /// </summary>
    public static IServiceCollection AddCrossMacroSharedPostPlatformRuntimeServices(
        this IServiceCollection services,
        Func<IServiceProvider, InputSimulatorPool?> simulatorPoolResolver)
    {
        ArgumentNullException.ThrowIfNull(simulatorPoolResolver);

        RegisterInputRuntimePrimitiveServices(services);
        RegisterPostPlatformPersistenceServices(services);
        RegisterScreenReadingServices(services);
        RegisterPlaybackAndHotkeyOrchestrationServices(services, simulatorPoolResolver);
        RegisterSchedulingAndShortcutServices(services);
        RegisterTextExpansionServices(services);
        RegisterEditorAndCaptureServices(services);
        RegisterProfileManagementServices(services);

        return services;
    }

    private static void RegisterPersistenceAndConfigurationServices(IServiceCollection services)
    {
        // Shared persisted configuration and file-backed state.
        services.AddSingleton<IHotkeyConfigurationService, HotkeyConfigurationService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<HotkeySettings>(sp =>
            sp.GetRequiredService<IHotkeyConfigurationService>().Load());
    }

    private static void RegisterRuntimePrimitiveServices(IServiceCollection services)
    {
        // Shared process-local runtime state and timing primitives.
        services.AddSingleton<IRuntimeContext, RuntimeContext>();
        services.AddSingleton<IRuntimeLogLevelService, RuntimeLogLevelService>();
        services.AddSingleton<ITimeProvider, SystemTimeProvider>();
    }

    private static void RegisterRecordingServices(IServiceCollection services)
    {
        // Recording stays in the common bucket because it tolerates optional
        // platform seams through late-bound factories.
        services.AddSingleton<Func<ICoordinateStrategy, IInputEventProcessor>>(
            _ => strategy => new StandardInputEventProcessor(strategy));

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
    }

    private static void RegisterInputRuntimePrimitiveServices(IServiceCollection services)
    {
        // Shared input parsing and mapping implementations that depend on the
        // moved platform/runtime contract allow-list rather than Core ownership.
        services.AddSingleton<IKeyCodeMapper, KeyCodeMapper>();
        services.AddSingleton<Func<IKeyCodeMapper>>(sp => sp.GetRequiredService<IKeyCodeMapper>);
        services.AddSingleton<IMouseButtonMapper, MouseButtonMapper>();
        services.AddSingleton<IModifierStateTracker, ModifierStateTracker>();
        services.AddSingleton<IHotkeyParser, HotkeyParser>();
        services.AddSingleton<IHotkeyStringBuilder, HotkeyStringBuilder>();
        services.AddSingleton<IHotkeyMatcher, HotkeyMatcher>();
    }

    private static void RegisterPostPlatformPersistenceServices(IServiceCollection services)
    {
        // Macro saving validates script key names with the platform-aware mapper.
        services.AddSingleton<IMacroFileManager, MacroFileManager>();
    }

    private static void RegisterScreenReadingServices(IServiceCollection services)
    {
        services.TryAddSingleton<IScreenFrameProvider, UnsupportedScreenFrameProvider>();
        services.TryAddSingleton<IScreenPixelReader, ScreenPixelReader>();
        services.TryAddSingleton<IScreenReadingWarmupService, ScreenReadingWarmupService>();
    }

    private static void RegisterPlaybackAndHotkeyOrchestrationServices(
        IServiceCollection services,
        Func<IServiceProvider, InputSimulatorPool?> simulatorPoolResolver)
    {
        // Playback and hotkey orchestration run after platform wiring because
        // they require platform-provided capture, position, and simulation seams.
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

        services.AddTransient<IMacroPlayer>(sp =>
        {
            var positionProvider = sp.GetRequiredService<IMousePositionProvider>();
            var validator = sp.GetRequiredService<PlaybackValidator>();
            var factory = sp.GetService<Func<IInputSimulator>>();
            var pool = simulatorPoolResolver(sp);
            var playbackBehaviorPolicy = sp.GetService<IPlaybackBehaviorPolicy>();

            return new MacroPlayer(
                positionProvider,
                validator,
                inputSimulatorFactory: factory,
                simulatorPool: pool,
                playbackBehaviorPolicy: playbackBehaviorPolicy,
                screenPixelReader: sp.GetRequiredService<IScreenPixelReader>(),
                keyCodeMapper: sp.GetRequiredService<IKeyCodeMapper>(),
                windowManager: sp.GetService<IWindowManager>());
        });

        services.AddSingleton<Func<IMacroPlayer>>(sp => () => sp.GetRequiredService<IMacroPlayer>());
    }

    private static void RegisterSchedulingAndShortcutServices(IServiceCollection services)
    {
        // Shared scheduling and shortcut runtime services.
        services.AddSingleton<IScheduledTaskRepository, JsonScheduledTaskRepository>();
        services.AddSingleton<IScheduledTaskExecutor, MacroScheduledTaskExecutor>();
        services.AddSingleton<ISchedulerService, SchedulerService>();
        services.AddSingleton<IShortcutService, ShortcutService>();
    }

    private static void RegisterTextExpansionServices(IServiceCollection services)
    {
        // Shared text-expansion runtime services.
        services.AddSingleton<ITextExpansionStorageService, TextExpansionStorageService>();
        services.AddSingleton<IInputProcessor, InputProcessor>();
        services.AddSingleton<ITextBufferState, TextBufferState>();
        services.AddSingleton<ITextExpansionExecutor, TextExpansionExecutor>();
        services.AddSingleton<ITextExpansionService, TextExpansionService>();
    }

    private static void RegisterEditorAndCaptureServices(IServiceCollection services)
    {
        // Shared editor conversion and coordinate capture helpers.
        services.AddSingleton<IEditorActionConverter, EditorActionConverter>();
        services.AddSingleton<IEditorActionValidator, EditorActionValidator>();
        services.AddSingleton<ICoordinateCaptureService>(sp =>
        {
            var positionProvider = sp.GetRequiredService<IMousePositionProvider>();
            var captureFactory = sp.GetService<Func<IInputCapture>>();
            return new CoordinateCaptureService(positionProvider, captureFactory);
        });
    }

    private static void RegisterProfileManagementServices(IServiceCollection services)
    {
        services.AddSingleton<IProfileManager>(sp =>
        {
            var hasKeyboardLayout = sp.GetService<IKeyboardLayoutService>() != null;
            var hasInputCaptureFactory = sp.GetService<Func<IInputCapture>>() != null;

            return new ProfileManager(
                configRootPath: null,
                sp.GetRequiredService<ISettingsService>(),
                sp.GetRequiredService<IHotkeyConfigurationService>(),
                sp.GetRequiredService<HotkeySettings>(),
                hasKeyboardLayout ? sp.GetRequiredService<IGlobalHotkeyService>() : null,
                hasKeyboardLayout ? sp.GetRequiredService<IShortcutService>() : null,
                sp.GetRequiredService<ISchedulerService>(),
                hasInputCaptureFactory ? sp.GetRequiredService<ITextExpansionService>() : null,
                sp.GetRequiredService<IScheduledTaskRepository>(),
                sp.GetRequiredService<ITextExpansionStorageService>());
        });
    }
}
