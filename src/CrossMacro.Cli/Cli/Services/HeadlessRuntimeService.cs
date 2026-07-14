using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Application.Runtime;
using CrossMacro.Core.Services;
using CrossMacro.Core.Logging;
using CrossMacro.Platform.Abstractions;
using CrossMacro.Cli.Serialization;

namespace CrossMacro.Cli.Services;

public sealed class HeadlessRuntimeService : IHeadlessRuntimeService
{
    private readonly IDisplaySessionService _displaySessionService;
    private readonly ISettingsService _settingsService;
    private readonly IGlobalHotkeyService _globalHotkeyService;
    private readonly ISchedulerService _schedulerService;
    private readonly IShortcutService _shortcutService;
    private readonly ITextExpansionService _textExpansionService;
    private readonly IHeadlessHotkeyActionService _headlessHotkeyActionService;
    private readonly IScreenReadingWarmupService? _screenReadingWarmupService;
    private readonly IRuntimeLifecycle _runtimeLifecycle;

    public HeadlessRuntimeService(
        IDisplaySessionService displaySessionService,
        ISettingsService settingsService,
        IGlobalHotkeyService globalHotkeyService,
        ISchedulerService schedulerService,
        IShortcutService shortcutService,
        ITextExpansionService textExpansionService,
        IHeadlessHotkeyActionService headlessHotkeyActionService,
        IScreenReadingWarmupService? screenReadingWarmupService = null,
        IRuntimeLifecycle? runtimeLifecycle = null)
    {
        _displaySessionService = displaySessionService;
        _settingsService = settingsService;
        _globalHotkeyService = globalHotkeyService;
        _schedulerService = schedulerService;
        _shortcutService = shortcutService;
        _textExpansionService = textExpansionService;
        _headlessHotkeyActionService = headlessHotkeyActionService;
        _screenReadingWarmupService = screenReadingWarmupService;
        _runtimeLifecycle = runtimeLifecycle ?? CreateLifecycle(
            globalHotkeyService,
            schedulerService,
            shortcutService,
            textExpansionService,
            headlessHotkeyActionService,
            screenReadingWarmupService);
    }

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
            new RuntimeLifecycleStep("global hotkeys", _ =>
            {
                globalHotkeyService.Start();
                return Task.CompletedTask;
            }, _ =>
            {
                globalHotkeyService.Stop();
                return Task.CompletedTask;
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
                shortcutService.Stop();
                return Task.CompletedTask;
            }),
            new RuntimeLifecycleStep("text expansion", _ =>
            {
                textExpansionService.Start();
                return Task.CompletedTask;
            }, _ =>
            {
                if (textExpansionService.IsRunning)
                {
                    textExpansionService.Stop();
                }

                return Task.CompletedTask;
            }),
            new RuntimeLifecycleStep("headless hotkey actions", _ =>
            {
                headlessHotkeyActionService.Start();
                return Task.CompletedTask;
            }, token => headlessHotkeyActionService.StopAsync(token)),
            new RuntimeLifecycleStep("screen reading warmup", token => screenReadingWarmupService == null
                ? Task.CompletedTask
                : screenReadingWarmupService.WarmUpPortalSessionAsync(token), _ => Task.CompletedTask)
        ]);
    }

    public async Task<HeadlessRuntimeResult> RunAsync(CancellationToken cancellationToken)
    {
        var warnings = new List<string>();
        var lifecycle = _runtimeLifecycle;

        try
        {
            if (!_displaySessionService.IsSessionSupported(out var reason))
            {
                return Fail(
                    CliExitCode.EnvironmentError,
                    "Headless mode cannot start in this display session.",
                    [$"{reason}"]);
            }

            _settingsService.Load();

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
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return new HeadlessRuntimeResult
                {
                    Success = false,
                    ExitCode = CliExitCode.Cancelled,
                    Message = "Headless mode interrupted.",
                    Warnings = warnings,
                    Data = data
                };
            }

            return new HeadlessRuntimeResult
            {
                Success = true,
                ExitCode = CliExitCode.Success,
                Message = "Headless mode stopped.",
                Warnings = warnings,
                Data = data
            };
        }
        catch (OperationCanceledException)
        {
            return new HeadlessRuntimeResult
            {
                Success = false,
                ExitCode = CliExitCode.Cancelled,
                Message = "Headless mode interrupted.",
                Warnings = warnings
            };
        }
        catch (Exception ex)
        {
            return Fail(
                CliExitCode.EnvironmentError,
                "Failed to start headless mode.",
                [ex.Message],
                warnings);
        }
        finally
        {
            if (lifecycle != null)
            {
                try
                {
                    await lifecycle.StopAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Headless runtime shutdown failed");
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
            Warnings = warnings ?? []
        };
    }
}
