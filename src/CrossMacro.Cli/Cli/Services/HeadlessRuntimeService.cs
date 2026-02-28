using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Services;

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

    public HeadlessRuntimeService(
        IDisplaySessionService displaySessionService,
        ISettingsService settingsService,
        IGlobalHotkeyService globalHotkeyService,
        ISchedulerService schedulerService,
        IShortcutService shortcutService,
        ITextExpansionService textExpansionService,
        IHeadlessHotkeyActionService headlessHotkeyActionService)
    {
        _displaySessionService = displaySessionService;
        _settingsService = settingsService;
        _globalHotkeyService = globalHotkeyService;
        _schedulerService = schedulerService;
        _shortcutService = shortcutService;
        _textExpansionService = textExpansionService;
        _headlessHotkeyActionService = headlessHotkeyActionService;
    }

    public async Task<HeadlessRuntimeResult> RunAsync(CancellationToken cancellationToken)
    {
        var warnings = new List<string>();
        var hotkeysStarted = false;
        var schedulerStarted = false;
        var shortcutsStarted = false;
        var textExpansionStarted = false;
        var hotkeyActionsStarted = false;

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

            _globalHotkeyService.Start();
            hotkeysStarted = true;

            await _schedulerService.LoadAsync();
            _schedulerService.Start();
            schedulerStarted = true;

            await _shortcutService.LoadAsync();
            _shortcutService.Start();
            shortcutsStarted = true;

            _textExpansionService.Start();
            textExpansionStarted = _textExpansionService.IsRunning;

            _headlessHotkeyActionService.Start();
            hotkeyActionsStarted = _headlessHotkeyActionService.IsRunning;

            var data = new
            {
                globalHotkeys = _globalHotkeyService.IsRunning,
                scheduler = _schedulerService.IsRunning,
                shortcuts = _shortcutService.IsListening,
                textExpansion = _textExpansionService.IsRunning,
                hotkeyActions = _headlessHotkeyActionService.IsRunning
            };

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
            if (hotkeyActionsStarted)
            {
                TryStop(() => _headlessHotkeyActionService.Stop());
            }

            if (textExpansionStarted)
            {
                TryStop(() => _textExpansionService.Stop());
            }

            if (shortcutsStarted)
            {
                TryStop(() => _shortcutService.Stop());
            }

            if (schedulerStarted)
            {
                TryStop(() => _schedulerService.Stop());
            }

            if (hotkeysStarted)
            {
                TryStop(() => _globalHotkeyService.Stop());
            }
        }
    }

    private static void TryStop(Action stopAction)
    {
        try
        {
            stopAction();
        }
        catch
        {
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
