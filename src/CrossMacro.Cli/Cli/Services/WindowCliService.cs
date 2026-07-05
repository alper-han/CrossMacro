using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Cli.Services;

public sealed class WindowCliService : IWindowCliService
{
    private static readonly TimeSpan WaitPollInterval = TimeSpan.FromMilliseconds(200);
    private readonly IWindowManager? _windowManager;

    public WindowCliService(IWindowManager? windowManager)
    {
        _windowManager = windowManager;
    }

    public async Task<CliCommandExecutionResult> ExecuteAsync(WindowCliOptions options, CancellationToken cancellationToken)
    {
        if (!TryGetWindowManager(out var windowManager, out var unsupported))
        {
            return unsupported;
        }

        return options.Action switch
        {
            WindowCliAction.Active => await ActiveAsync(windowManager, cancellationToken).ConfigureAwait(false),
            WindowCliAction.List => await ListAsync(windowManager, cancellationToken).ConfigureAwait(false),
            WindowCliAction.Search when options.Selector is { } selector => await SearchAsync(windowManager, selector, cancellationToken).ConfigureAwait(false),
            WindowCliAction.Wait when options.Selector is { } selector => await WaitAsync(windowManager, selector, options.TimeoutMs ?? 5000, cancellationToken).ConfigureAwait(false),
            WindowCliAction.Focus when options.Selector is { } selector => await FocusAsync(windowManager, selector, cancellationToken).ConfigureAwait(false),
            WindowCliAction.Close when options.Selector is { Kind: WindowSelectorKind.Address or WindowSelectorKind.Title } selector => await CloseAsync(windowManager, selector, cancellationToken).ConfigureAwait(false),
            WindowCliAction.Move when options.X is int x && options.Y is int y => await MutationAsync("move", () => windowManager.MoveActiveWindowAsync(x, y, cancellationToken)).ConfigureAwait(false),
            WindowCliAction.Resize when options.Width is int width && options.Height is int height => await MutationAsync("resize", () => windowManager.ResizeActiveWindowAsync(width, height, cancellationToken)).ConfigureAwait(false),
            WindowCliAction.Center => await MutationAsync("center", () => windowManager.CenterActiveWindowAsync(cancellationToken)).ConfigureAwait(false),
            WindowCliAction.Maximize => await MutationAsync("maximize", () => windowManager.MaximizeActiveWindowAsync(cancellationToken)).ConfigureAwait(false),
            WindowCliAction.Fullscreen => await MutationAsync("fullscreen", () => windowManager.FullscreenActiveWindowAsync(cancellationToken)).ConfigureAwait(false),
            WindowCliAction.Float => await MutationAsync("float", () => windowManager.FloatActiveWindowAsync(cancellationToken)).ConfigureAwait(false),
            WindowCliAction.WorkspaceGet => await WorkspaceGetAsync(windowManager, cancellationToken).ConfigureAwait(false),
            WindowCliAction.WorkspaceSwitch when !string.IsNullOrWhiteSpace(options.WorkspaceName) => await MutationAsync("workspace switch", () => windowManager.SwitchWorkspaceAsync(options.WorkspaceName, cancellationToken)).ConfigureAwait(false),
            WindowCliAction.WorkspaceMoveActive when !string.IsNullOrWhiteSpace(options.WorkspaceName) => await MutationAsync("workspace move-active", () => windowManager.MoveActiveWindowToWorkspaceAsync(options.WorkspaceName, cancellationToken)).ConfigureAwait(false),
            WindowCliAction.WorkspaceMoveWindow when options.Selector is { Kind: WindowSelectorKind.Address } selector && !string.IsNullOrWhiteSpace(options.WorkspaceName) => await MutationAsync("workspace move-window", () => windowManager.MoveWindowToWorkspaceByAddressAsync(selector.Value, options.WorkspaceName, cancellationToken)).ConfigureAwait(false),
            WindowCliAction.Search or WindowCliAction.Wait or WindowCliAction.Focus or WindowCliAction.Close or WindowCliAction.Move or WindowCliAction.Resize or WindowCliAction.WorkspaceSwitch or WindowCliAction.WorkspaceMoveActive or WindowCliAction.WorkspaceMoveWindow => InvalidOptions(options.Action),
            _ => CliCommandExecutionResult.Fail(CliExitCode.InvalidArguments, "Unknown window action.")
        };
    }

    private static async Task<CliCommandExecutionResult> ActiveAsync(IWindowManager windowManager, CancellationToken cancellationToken)
    {
        var active = await windowManager.GetActiveWindowAsync(cancellationToken).ConfigureAwait(false);
        return active is null
            ? CliCommandExecutionResult.Fail(CliExitCode.RuntimeError, "No active window was reported by the window manager.")
            : CliCommandExecutionResult.Ok("Active window read.", Map(active));
    }

    private static async Task<CliCommandExecutionResult> ListAsync(IWindowManager windowManager, CancellationToken cancellationToken)
    {
        var windows = await windowManager.GetWindowsAsync(cancellationToken).ConfigureAwait(false);
        var data = new WindowListData(windows.Select(Map).ToArray(), windows.Count);
        return CliCommandExecutionResult.Ok("Windows listed.", data);
    }

    private static async Task<CliCommandExecutionResult> SearchAsync(IWindowManager windowManager, WindowSelector selector, CancellationToken cancellationToken)
    {
        var windows = await windowManager.GetWindowsAsync(cancellationToken).ConfigureAwait(false);
        var matches = Match(windows, selector).Select(Map).ToArray();
        return CliCommandExecutionResult.Ok("Window search complete.", new WindowListData(matches, matches.Length));
    }

    private static async Task<CliCommandExecutionResult> WaitAsync(IWindowManager windowManager, WindowSelector selector, int timeoutMs, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMs);
        WindowInfo? match = null;

        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            var windows = await windowManager.GetWindowsAsync(cancellationToken).ConfigureAwait(false);
            match = Match(windows, selector).FirstOrDefault();
            if (match is not null)
            {
                break;
            }

            if (DateTimeOffset.UtcNow >= deadline)
            {
                break;
            }

            await Task.Delay(WaitPollInterval, cancellationToken).ConfigureAwait(false);
        } while (true);

        return CliCommandExecutionResult.Ok(
            match is null ? "Window wait timed out." : "Window wait matched.",
            new WindowWaitData(match is not null, match is null ? null : Map(match), timeoutMs));
    }

    private static async Task<CliCommandExecutionResult> FocusAsync(IWindowManager windowManager, WindowSelector selector, CancellationToken cancellationToken)
    {
        return await MutationAsync("focus", () => selector.Kind switch
        {
            WindowSelectorKind.Address => windowManager.FocusWindowByAddressAsync(selector.Value, cancellationToken),
            WindowSelectorKind.Title => windowManager.FocusWindowByTitleAsync(selector.Value, cancellationToken),
            WindowSelectorKind.Class => windowManager.FocusWindowByClassAsync(selector.Value, cancellationToken),
            _ => Task.FromResult(false)
        }).ConfigureAwait(false);
    }

    private static async Task<CliCommandExecutionResult> CloseAsync(IWindowManager windowManager, WindowSelector selector, CancellationToken cancellationToken)
    {
        return await MutationAsync("close", () => selector.Kind switch
        {
            WindowSelectorKind.Address => windowManager.CloseWindowByAddressAsync(selector.Value, cancellationToken),
            WindowSelectorKind.Title => windowManager.CloseWindowByTitleAsync(selector.Value, cancellationToken),
            _ => Task.FromResult(false)
        }).ConfigureAwait(false);
    }

    private static async Task<CliCommandExecutionResult> WorkspaceGetAsync(IWindowManager windowManager, CancellationToken cancellationToken)
    {
        var workspace = await windowManager.GetActiveWorkspaceAsync(cancellationToken).ConfigureAwait(false);
        return workspace is null
            ? CliCommandExecutionResult.Fail(CliExitCode.RuntimeError, "No active workspace was reported by the window manager.")
            : CliCommandExecutionResult.Ok("Active workspace read.", new WorkspaceData(workspace));
    }

    private static IEnumerable<WindowInfo> Match(IEnumerable<WindowInfo> windows, WindowSelector selector)
    {
        return selector.Kind switch
        {
            WindowSelectorKind.Title => windows.Where(w => w.Title.Contains(selector.Value, StringComparison.OrdinalIgnoreCase)),
            WindowSelectorKind.Class => windows.Where(w => w.Class.Contains(selector.Value, StringComparison.OrdinalIgnoreCase)),
            WindowSelectorKind.Address => windows.Where(w => string.Equals(w.Address, selector.Value, StringComparison.OrdinalIgnoreCase)),
            _ => []
        };
    }

    private static async Task<CliCommandExecutionResult> MutationAsync(string operation, Func<Task<bool>> mutate)
    {
        var result = await mutate().ConfigureAwait(false);
        return result
            ? CliCommandExecutionResult.Ok($"Window {operation} complete.", new WindowMutationData(operation, true))
            : CliCommandExecutionResult.Fail(
                CliExitCode.RuntimeError,
                $"Window {operation} failed.",
                ["The window manager did not report success. The target may not exist or the operation may be unsupported."],
                data: new WindowMutationData(operation, false));
    }

    private bool TryGetWindowManager(
        [NotNullWhen(true)] out IWindowManager? windowManager,
        [NotNullWhen(false)] out CliCommandExecutionResult? result)
    {
        if (_windowManager is null || !_windowManager.IsSupported)
        {
            windowManager = null;
            result = CliCommandExecutionResult.Fail(
                CliExitCode.EnvironmentError,
                "Window management is not supported in this runtime.",
                ["No supported IWindowManager is available for the current platform/session."]);
            return false;
        }

        windowManager = _windowManager;
        result = null;
        return true;
    }

    private static CliCommandExecutionResult InvalidOptions(WindowCliAction action) =>
        CliCommandExecutionResult.Fail(CliExitCode.InvalidArguments, $"Invalid options for window action '{action}'.");

    private static WindowInfoData Map(WindowInfo window) => new(
        window.Address,
        window.Title,
        window.Class,
        window.Pid,
        window.Workspace,
        window.IsFocused,
        window.IsFullscreen,
        window.IsMaximized,
        window.IsFloating,
        window.IsPinned,
        window.IsHidden,
        window.X,
        window.Y,
        window.Width,
        window.Height);
}
