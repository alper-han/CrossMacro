using System;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Core.Services.Recording.Processors;
using CrossMacro.Core.Services.Recording.Strategies;
using CrossMacro.Core.Services.TextExpansion;
using CrossMacro.Infrastructure.Services;
using CrossMacro.Infrastructure.Services.TextExpansion;
using Microsoft.Extensions.DependencyInjection;

namespace CrossMacro.Infrastructure.DependencyInjection;

/// <summary>
/// Shared runtime DI registrations consumed by both GUI and CLI hosts.
/// </summary>
public static class RuntimeServiceCollectionExtensions
{
    public static IServiceCollection AddCrossMacroCommonRuntimeServices(this IServiceCollection services)
    {
        services.AddSingleton<IRuntimeContext, RuntimeContext>();
        services.AddSingleton<IHotkeyConfigurationService, HotkeyConfigurationService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<HotkeySettings>(sp =>
            sp.GetRequiredService<IHotkeyConfigurationService>().Load());
        services.AddSingleton<ITimeProvider, SystemTimeProvider>();
        services.AddSingleton<IMacroFileManager, MacroFileManager>();

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

        return services;
    }

    public static IServiceCollection AddCrossMacroSharedPostPlatformRuntimeServices(
        this IServiceCollection services,
        Func<IServiceProvider, InputSimulatorPool?> simulatorPoolResolver)
    {
        ArgumentNullException.ThrowIfNull(simulatorPoolResolver);

        services.AddSingleton<IKeyCodeMapper, KeyCodeMapper>();
        services.AddSingleton<IMouseButtonMapper, MouseButtonMapper>();
        services.AddSingleton<IModifierStateTracker, ModifierStateTracker>();
        services.AddSingleton<IHotkeyParser, HotkeyParser>();
        services.AddSingleton<IHotkeyStringBuilder, HotkeyStringBuilder>();
        services.AddSingleton<IHotkeyMatcher, HotkeyMatcher>();

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
                playbackBehaviorPolicy: playbackBehaviorPolicy);
        });

        services.AddSingleton<Func<IMacroPlayer>>(sp => () => sp.GetRequiredService<IMacroPlayer>());

        services.AddSingleton<IScheduledTaskRepository, JsonScheduledTaskRepository>();
        services.AddSingleton<IScheduledTaskExecutor, MacroScheduledTaskExecutor>();
        services.AddSingleton<ISchedulerService, SchedulerService>();
        services.AddSingleton<IShortcutService, ShortcutService>();
        services.AddSingleton<ITextExpansionStorageService, TextExpansionStorageService>();

        services.AddSingleton<IInputProcessor, InputProcessor>();
        services.AddSingleton<ITextBufferState, TextBufferState>();
        services.AddSingleton<ITextExpansionExecutor, TextExpansionExecutor>();
        services.AddSingleton<ITextExpansionService, TextExpansionService>();

        services.AddSingleton<IEditorActionConverter, EditorActionConverter>();
        services.AddSingleton<IEditorActionValidator, EditorActionValidator>();
        services.AddSingleton<ICoordinateCaptureService>(sp =>
        {
            var positionProvider = sp.GetRequiredService<IMousePositionProvider>();
            var captureFactory = sp.GetService<Func<IInputCapture>>();
            return new CoordinateCaptureService(positionProvider, captureFactory);
        });

        return services;
    }
}
