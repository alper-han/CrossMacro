
namespace CrossMacro.Cli.Services;

public sealed class HeadlessRuntimeService(
    IDisplaySessionService displaySessionService,
    ISettingsService settingsService,
    IGlobalHotkeyService globalHotkeyService,
    ISchedulerService schedulerService,
    IShortcutService shortcutService,
    ITextExpansionService textExpansionService,
    IHeadlessHotkeyActionService headlessHotkeyActionService,
    IScreenReadingWarmupService? screenReadingWarmupService = null,
    IRuntimeLifecycle? runtimeLifecycle = null) : IHeadlessRuntimeService
{
    private readonly IDisplaySessionService _displaySessionService = displaySessionService;
    private readonly ISettingsService _settingsService = settingsService;
    private readonly IGlobalHotkeyService _globalHotkeyService = globalHotkeyService;
    private readonly ISchedulerService _schedulerService = schedulerService;
    private readonly IShortcutService _shortcutService = shortcutService;
    private readonly ITextExpansionService _textExpansionService = textExpansionService;
    private readonly IHeadlessHotkeyActionService _headlessHotkeyActionService = headlessHotkeyActionService;
    private readonly IRuntimeLifecycle _runtimeLifecycle = runtimeLifecycle ?? CreateLifecycle(
            globalHotkeyService,
            schedulerService,
            shortcutService,
            textExpansionService,
            headlessHotkeyActionService,
            screenReadingWarmupService);

    internal static IRuntimeLifecycle CreateLifecycle(
        IGlobalHotkeyService globalHotkeyService,
        ISchedulerService schedulerService,
        IShortcutService shortcutService,
        ITextExpansionService textExpansionService,
        IHeadlessHotkeyActionService headlessHotkeyActionService,
        IScreenReadingWarmupService? screenReadingWarmupService)
    {
        return new RuntimeLifecycle(
        [
            new RuntimeLifecycleStep("global hotkeys", async cancellationToken =>
            {
                await globalHotkeyService.InitializeAsync(cancellationToken).ConfigureAwait(false);
                globalHotkeyService.Start();
            }, cancellationToken =>
            {
                return globalHotkeyService.StopHotkeyServiceAsync(cancellationToken);
            }),
            new RuntimeLifecycleStep("scheduler", async _ =>
            {
                await schedulerService.LoadAsync().ConfigureAwait(false);
                schedulerService.Start();
            }, token => schedulerService.StopAsync(token)),
            new RuntimeLifecycleStep("shortcuts", async _ =>
            {
                await shortcutService.LoadAsync().ConfigureAwait(false);
                shortcutService.Start();
            }, _ =>
            {
        shortcutService.StopShortcuts();
                return Task.CompletedTask;
            }),
            new RuntimeLifecycleStep("text expansion", cancellationToken =>
            {
                return textExpansionService.StartAsync(cancellationToken);
            }, cancellationToken =>
            {
                if (textExpansionService.IsRunning)
                {
                    return textExpansionService.StopExpansionAsync(cancellationToken);
                }

                return Task.CompletedTask;
            }),
            new RuntimeLifecycleStep("headless hotkey actions", _ =>
            {
                headlessHotkeyActionService.Start();
                return Task.CompletedTask;
            }, token => headlessHotkeyActionService.StopAsync(token)),
            new RuntimeLifecycleStep("screen reading warmup", token => screenReadingWarmupService is null
                ? Task.CompletedTask
                : screenReadingWarmupService.WarmUpPortalSessionAsync(token), _ => Task.CompletedTask),
        ]);
    }

    public async Task<HeadlessRuntimeResult> RunAsync(CancellationToken cancellationToken)
    {
        var warnings = new List<string>();
        var lifecycle = _runtimeLifecycle;

        try
        {
            var sessionSupport = await _displaySessionService.IsSessionSupportedAsync(cancellationToken).ConfigureAwait(false);
            if (!sessionSupport.Supported)
            {
                return Fail(
                    CliExitCode.EnvironmentError,
                    "Headless mode cannot start in this display session.",
                    [$"{sessionSupport.Reason}"]);
            }

            _ = await _settingsService.LoadAsync().ConfigureAwait(false);

            await lifecycle.StartAsync(cancellationToken).ConfigureAwait(false);

            var data = new HeadlessRuntimeData(
                _globalHotkeyService.IsRunning,
                _schedulerService.IsRunning,
                _shortcutService.IsListening,
                _textExpansionService.IsRunning,
                _headlessHotkeyActionService.IsRunning
            );

            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return new HeadlessRuntimeResult
                {
                    Success = false,
                    ExitCode = CliExitCode.Cancelled,
                    Message = "Headless mode interrupted.",
                    Warnings = warnings,
                    Data = data,
                };
            }

            return new HeadlessRuntimeResult
            {
                Success = true,
                ExitCode = CliExitCode.Success,
                Message = "Headless mode stopped.",
                Warnings = warnings,
                Data = data,
            };
        }
        catch (OperationCanceledException)
        {
            return new HeadlessRuntimeResult
            {
                Success = false,
                ExitCode = CliExitCode.Cancelled,
                Message = "Headless mode interrupted.",
                Warnings = warnings,
            };
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return Fail(
                CliExitCode.EnvironmentError,
                "Failed to start headless mode.",
                [ex.Message],
                warnings);
        }
        finally
        {
            if (lifecycle is not null)
            {
                try
                {
                    await lifecycle.StopAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    Log.LogError(ex, "Headless runtime shutdown failed");
                }
            }
        }
    }

    private static HeadlessRuntimeResult Fail(
        CliExitCode exitCode,
        string message,
        IReadOnlyList<string>? errors = null,
        IReadOnlyList<string>? warnings = null)
    {
        return new HeadlessRuntimeResult
        {
            Success = false,
            ExitCode = exitCode,
            Message = message,
            Errors = errors ?? [],
            Warnings = warnings ?? [],
        };
    }
}
