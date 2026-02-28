using System;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Core.Services.Recording.Processors;
using CrossMacro.Core.Services.Recording.Strategies;
using CrossMacro.Core.Services.TextExpansion;
using CrossMacro.Infrastructure.Services;
using CrossMacro.Infrastructure.Services.TextExpansion;
using Microsoft.Extensions.DependencyInjection;

namespace CrossMacro.Cli.DependencyInjection;

public static class CliRuntimeServiceCollectionExtensions
{
    public static IServiceCollection AddCrossMacroCliRuntimeServices(
        this IServiceCollection services,
        IPlatformServiceRegistrar platformServiceRegistrar,
        CliRuntimeProfile runtimeProfile = CliRuntimeProfile.OneShot)
    {
        ArgumentNullException.ThrowIfNull(platformServiceRegistrar);

        services.AddCommonServices();
        platformServiceRegistrar.RegisterPlatformServices(services);
        services.AddCliPostPlatformServices(runtimeProfile);

        return services;
    }

    private static IServiceCollection AddCommonServices(this IServiceCollection services)
    {
        services.AddSingleton<IHotkeyConfigurationService, HotkeyConfigurationService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<HotkeySettings>(sp =>
            sp.GetRequiredService<IHotkeyConfigurationService>().Load());
        services.AddSingleton<ITimeProvider, SystemTimeProvider>();
        services.AddSingleton<IMacroFileManager, MacroFileManager>();

        services.AddSingleton<Func<ICoordinateStrategy, IInputEventProcessor>>(sp =>
            strategy => new StandardInputEventProcessor(strategy));

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

    private static IServiceCollection AddCliPostPlatformServices(
        this IServiceCollection services,
        CliRuntimeProfile runtimeProfile)
    {
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
            var pool = runtimeProfile == CliRuntimeProfile.Persistent
                ? sp.GetService<InputSimulatorPool>()
                : null;
            return new MacroPlayer(positionProvider, validator, inputSimulatorFactory: factory, simulatorPool: pool);
        });

        services.AddSingleton<Func<IMacroPlayer>>(sp => () => sp.GetRequiredService<IMacroPlayer>());

        RegisterCliClipboardServices(services);

        services.AddSingleton<IScheduledTaskRepository, JsonScheduledTaskRepository>();
        services.AddSingleton<IScheduledTaskExecutor, MacroScheduledTaskExecutor>();
        services.AddSingleton<ISchedulerService, SchedulerService>();
        services.AddSingleton<IShortcutService, ShortcutService>();
        services.AddSingleton<ITextExpansionStorageService, TextExpansionStorageService>();

        services.AddTransient<IInputProcessor, InputProcessor>();
        services.AddTransient<ITextBufferState, TextBufferState>();
        services.AddTransient<ITextExpansionExecutor, TextExpansionExecutor>();
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

    private static void RegisterCliClipboardServices(IServiceCollection services)
    {
        if (OperatingSystem.IsLinux())
        {
            services.AddSingleton<IProcessRunner, ProcessRunner>();
            services.AddSingleton<LinuxShellClipboardService>();
            services.AddSingleton<IClipboardService>(sp => sp.GetRequiredService<LinuxShellClipboardService>());
            return;
        }

        services.AddSingleton<IClipboardService, CliNoOpClipboardService>();
    }

    private sealed class CliNoOpClipboardService : IClipboardService
    {
        public bool IsSupported => false;

        public Task SetTextAsync(string text)
        {
            return Task.CompletedTask;
        }

        public Task<string?> GetTextAsync()
        {
            return Task.FromResult<string?>(null);
        }
    }
}
