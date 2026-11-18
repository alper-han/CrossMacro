using CrossMacro.Application.DependencyInjection;
using CrossMacro.Application.Settings;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrossMacro.Commands.DependencyInjection;

/// <summary>Registers the command services once for every host that executes CrossMacro commands.</summary>
public static class CommandServiceCollectionExtensions
{
    public static IServiceCollection AddCrossMacroCommandServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        _ = services.AddCrossMacroApplicationServices();
        services.TryAddSingleton<IMacroExecutionService, MacroExecutionService>();
        services.TryAddSingleton<IRecordExecutionService, RecordExecutionService>();
        services.TryAddSingleton<IRunScriptExecutionService>(sp => new RunScriptExecutionService(
            sp.GetRequiredService<IRunExecutionService>(),
            sp.GetRequiredService<IImageAssetCodec>()));
        services.TryAddSingleton<IPreflightService>(sp => new PreflightService(
            sp.GetRequiredService<IRuntimeContext>(),
            sp.GetRequiredService<IDisplaySessionService>(),
            sp.GetRequiredService<Func<IInputSimulator>>(),
            sp.GetRequiredService<Func<IInputCapture>>()));
        services.TryAddSingleton<IDoctorService>(sp => new DoctorService(
            sp.GetRequiredService<IRuntimeContext>(),
            sp.GetRequiredService<IDisplayEnvironmentDiagnostic>(),
            sp.GetRequiredService<IEnvironmentInfoProvider>(),
            sp.GetRequiredService<IDisplaySessionService>(),
            sp.GetRequiredService<Func<IInputSimulator>>(),
            sp.GetRequiredService<Func<IInputCapture>>(),
            sp.GetRequiredService<IMousePositionProvider>(),
            sp.GetRequiredService<ApplicationPaths>(),
            sp.GetService<IPermissionChecker>(),
            sp.GetService<ILinuxInputDiagnostics>(),
            sp.GetService<IScreenReadingDiagnosticProvider>(),
            sp.GetService<IMacOSScreenRecordingPermissionProbe>(),
            sp.GetService<IScreenReadingCapabilityReadiness>()));
        services.TryAddSingleton<ISettingsCommandService>(sp => new SettingsCommandService(
            sp.GetRequiredService<ISettingsService>(),
            sp.GetRequiredService<SettingsChangeCoordinator>(),
            sp.GetService<IPortalScreenCastRestoreStateService>()));
        services.TryAddSingleton<IQuickSetupCommandService, QuickSetupCommandService>();
        services.TryAddSingleton<IProfileCommandService, ProfileCommandService>();
        services.TryAddSingleton<ITextExpansionCommandService, TextExpansionCommandService>();
        services.TryAddSingleton<IClipboardCommandService>(sp => new ClipboardCommandService(sp.GetService<IClipboardService>()));
        services.TryAddSingleton<IWindowCommandService>(sp => new WindowCommandService(sp.GetService<IWindowManager>()));
        services.TryAddSingleton<IScreenCommandService>(sp => new ScreenCommandService(
            sp.GetService<IScreenPixelReader>(),
            sp.GetService<IMousePositionProvider>(),
            sp.GetService<IScreenImageAutomation>()));
        services.TryAddSingleton<IScreenshotCommandService>(sp => new ScreenshotCommandService(sp.GetRequiredService<IScreenshotCaptureService>()));
        return services;
    }
}
