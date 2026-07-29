namespace CrossMacro.Cli.DependencyInjection;

internal static class CliOperationalServiceRegistration
{
    internal static void Register(IServiceCollection services)
    {
        _ = services.AddSingleton<IRecordExecutionService, RecordExecutionService>();
        _ = services.AddSingleton<IHeadlessHotkeyActionService, HeadlessHotkeyActionService>();
        _ = services.AddSingleton<IRuntimeLifecycle>(sp => HeadlessRuntimeService.CreateLifecycle(sp.GetRequiredService<IGlobalHotkeyService>(), sp.GetRequiredService<ISchedulerService>(), sp.GetRequiredService<IShortcutService>(), sp.GetRequiredService<ITextExpansionService>(), sp.GetRequiredService<IHeadlessHotkeyActionService>(), sp.GetService<IScreenReadingWarmupService>()));
        _ = services.AddSingleton<IHeadlessRuntimeService, HeadlessRuntimeService>();
        _ = services.AddSingleton<IRunScriptExecutionService>(sp => new RunScriptExecutionService(sp.GetRequiredService<IRunExecutionService>()));
        _ = services.AddSingleton<IClipboardCliService>(static sp => new ClipboardCliService(sp.GetService<IClipboardService>()));
        _ = services.AddSingleton<IWindowCliService>(static sp => new WindowCliService(sp.GetService<IWindowManager>()));
        _ = services.AddSingleton<IScreenCliService>(sp => new ScreenCliService(sp.GetService<IScreenPixelReader>(), sp.GetService<IMousePositionProvider>(), sp.GetService<IScreenImageAutomation>()));
        _ = services.AddSingleton<IScreenshotCliService>(sp => new ScreenshotCliService(sp.GetRequiredService<IScreenshotCaptureService>()));
    }
}
