using CrossMacro.Platform.Abstractions.WindowManagement;

namespace CrossMacro.Commands.WindowManagement;

public sealed class WindowCommandService(IWindowManager? windowManager, TimeProvider? timeProvider = null) : IWindowCommandService
{
    private static readonly TimeSpan WaitPollInterval = TimeSpan.FromMilliseconds(200);
    private readonly IWindowManager? _windowManager = windowManager;
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<OperationResult> ExecuteAsync(WindowOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!TryGetWindowManager(out var resolvedWindowManager, out var unsupported))
        {
            return unsupported;
        }

        return options.Action switch
        {
            WindowAction.Active => await ActiveAsync(resolvedWindowManager, cancellationToken).ConfigureAwait(false),
            WindowAction.List => await ListAsync(resolvedWindowManager, cancellationToken).ConfigureAwait(false),
            WindowAction.Search when options.Selector is { } selector => await SearchAsync(resolvedWindowManager, selector, cancellationToken).ConfigureAwait(false),
            WindowAction.Wait when options.Selector is { } selector => await WaitAsync(resolvedWindowManager, selector, options.TimeoutMs ?? 5000, cancellationToken).ConfigureAwait(false),
            WindowAction.Focus when options.Selector is { } selector => await FocusAsync(resolvedWindowManager, selector, cancellationToken).ConfigureAwait(false),
            WindowAction.Close when options.Selector is { Kind: WindowSelectorKind.Address or WindowSelectorKind.Title } selector => await CloseAsync(resolvedWindowManager, selector, cancellationToken).ConfigureAwait(false),
            WindowAction.Move when options.X is int x && options.Y is int y => await MutationAsync("move", () => resolvedWindowManager.MoveActiveWindowAsync(x, y, cancellationToken)).ConfigureAwait(false),
            WindowAction.Resize when options.Width is int width && options.Height is int height => await MutationAsync("resize", () => resolvedWindowManager.ResizeActiveWindowAsync(width, height, cancellationToken)).ConfigureAwait(false),
            WindowAction.Center => await MutationAsync("center", () => resolvedWindowManager.CenterActiveWindowAsync(cancellationToken)).ConfigureAwait(false),
            WindowAction.Maximize => await MutationAsync("maximize", () => resolvedWindowManager.MaximizeActiveWindowAsync(cancellationToken)).ConfigureAwait(false),
            WindowAction.Fullscreen => await MutationAsync("fullscreen", () => resolvedWindowManager.FullscreenActiveWindowAsync(cancellationToken)).ConfigureAwait(false),
            WindowAction.Floating => await MutationAsync("float", () => resolvedWindowManager.FloatActiveWindowAsync(cancellationToken)).ConfigureAwait(false),
            WindowAction.WorkspaceGet => await WorkspaceGetAsync(resolvedWindowManager, cancellationToken).ConfigureAwait(false),
            WindowAction.WorkspaceSwitch when !string.IsNullOrWhiteSpace(options.WorkspaceName) => await MutationAsync("workspace switch", () => resolvedWindowManager.SwitchWorkspaceAsync(options.WorkspaceName, cancellationToken)).ConfigureAwait(false),
            WindowAction.WorkspaceMoveActive when !string.IsNullOrWhiteSpace(options.WorkspaceName) => await MutationAsync("workspace move-active", () => resolvedWindowManager.MoveActiveWindowToWorkspaceAsync(options.WorkspaceName, cancellationToken)).ConfigureAwait(false),
            WindowAction.WorkspaceMoveWindow when options.Selector is { Kind: WindowSelectorKind.Address } selector && !string.IsNullOrWhiteSpace(options.WorkspaceName) => await MutationAsync("workspace move-window", () => resolvedWindowManager.MoveWindowToWorkspaceByAddressAsync(selector.Value, options.WorkspaceName, cancellationToken)).ConfigureAwait(false),
            WindowAction.Search or WindowAction.Wait or WindowAction.Focus or WindowAction.Close or WindowAction.Move or WindowAction.Resize or WindowAction.WorkspaceSwitch or WindowAction.WorkspaceMoveActive or WindowAction.WorkspaceMoveWindow => InvalidOptions(options.Action),
            _ => OperationResult.Fail(ExecutionOutcomeCode.InvalidArguments, "Unknown window action."),
        };
    }

    private static async Task<OperationResult> ActiveAsync(IWindowManager windowManager, CancellationToken cancellationToken)
    {
        var active = await windowManager.GetActiveWindowAsync(cancellationToken).ConfigureAwait(false);
        return active is null
            ? OperationResult.Fail(ExecutionOutcomeCode.RuntimeError, "No active window was reported by the window manager.")
            : OperationResult.Ok("Active window read.", Map(active));
    }

    private static async Task<OperationResult> ListAsync(IWindowManager windowManager, CancellationToken cancellationToken)
    {
        var windows = await windowManager.GetWindowsAsync(cancellationToken).ConfigureAwait(false);
        var data = new WindowListData(windows.Select(Map).ToArray(), windows.Count);
        return OperationResult.Ok("Windows listed.", data);
    }

    private static async Task<OperationResult> SearchAsync(IWindowManager windowManager, WindowSelector selector, CancellationToken cancellationToken)
    {
        var windows = await windowManager.GetWindowsAsync(cancellationToken).ConfigureAwait(false);
        var matches = Match(windows, selector).Select(Map).ToArray();
        return OperationResult.Ok("Window search complete.", new WindowListData(matches, matches.Length));
    }

    private async Task<OperationResult> WaitAsync(IWindowManager windowManager, WindowSelector selector, int timeoutMs, CancellationToken cancellationToken)
    {
        var deadline = _timeProvider.GetUtcNow().AddMilliseconds(timeoutMs);
        WindowInfo? match;

        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            var windows = await windowManager.GetWindowsAsync(cancellationToken).ConfigureAwait(false);
            match = Match(windows, selector).FirstOrDefault();
            if (match is not null)
            {
                break;
            }

            if (_timeProvider.GetUtcNow() >= deadline)
            {
                break;
            }

            await Task.Delay(WaitPollInterval, _timeProvider, cancellationToken).ConfigureAwait(false);
        } while (true);

        return OperationResult.Ok(
            match is null ? "Window wait timed out." : "Window wait matched.",
            new WindowWaitData(match is not null, match is null ? null : Map(match), timeoutMs));
    }

    private static async Task<OperationResult> FocusAsync(IWindowManager windowManager, WindowSelector selector, CancellationToken cancellationToken)
    {
        return await MutationAsync("focus", () => selector.Kind switch
        {
            WindowSelectorKind.Address => windowManager.FocusWindowByAddressAsync(selector.Value, cancellationToken),
            WindowSelectorKind.Title => windowManager.FocusWindowByTitleAsync(selector.Value, cancellationToken),
            WindowSelectorKind.Class => windowManager.FocusWindowByClassAsync(selector.Value, cancellationToken),
            _ => Task.FromResult(false),
        }).ConfigureAwait(false);
    }

    private static async Task<OperationResult> CloseAsync(IWindowManager windowManager, WindowSelector selector, CancellationToken cancellationToken)
    {
        return await MutationAsync("close", () => selector.Kind switch
        {
            WindowSelectorKind.Address => windowManager.CloseWindowByAddressAsync(selector.Value, cancellationToken),
            WindowSelectorKind.Title => windowManager.CloseWindowByTitleAsync(selector.Value, cancellationToken),
            WindowSelectorKind.Class => Task.FromResult(false),
            _ => Task.FromResult(false),
        }).ConfigureAwait(false);
    }

    private static async Task<OperationResult> WorkspaceGetAsync(IWindowManager windowManager, CancellationToken cancellationToken)
    {
        var workspace = await windowManager.GetActiveWorkspaceAsync(cancellationToken).ConfigureAwait(false);
        return workspace is null
            ? OperationResult.Fail(ExecutionOutcomeCode.RuntimeError, "No active workspace was reported by the window manager.")
            : OperationResult.Ok("Active workspace read.", new WorkspaceData(workspace));
    }

    private static IEnumerable<WindowInfo> Match(IEnumerable<WindowInfo> windows, WindowSelector selector)
    {
        return selector.Kind switch
        {
            WindowSelectorKind.Title => windows.Where(w => w.Title.Contains(selector.Value, StringComparison.OrdinalIgnoreCase)),
            WindowSelectorKind.Class => windows.Where(w => w.Class.Contains(selector.Value, StringComparison.OrdinalIgnoreCase)),
            WindowSelectorKind.Address => windows.Where(w => string.Equals(w.Address, selector.Value, StringComparison.OrdinalIgnoreCase)),
            _ => [],
        };
    }

    private static async Task<OperationResult> MutationAsync(string operation, Func<Task<bool>> mutate)
    {
        var result = await mutate().ConfigureAwait(false);
        return result
            ? OperationResult.Ok($"Window {operation} complete.", new WindowMutationData(operation, Result: true))
            : OperationResult.Fail(
                ExecutionOutcomeCode.RuntimeError,
                $"Window {operation} failed.",
                ["The window manager did not report success. The target may not exist or the operation may be unsupported."],
                data: new WindowMutationData(operation, Result: false));
    }

    private bool TryGetWindowManager(
        [NotNullWhen(true)] out IWindowManager? windowManager,
        [NotNullWhen(false)] out OperationResult? result)
    {
        if (_windowManager is null || !_windowManager.IsSupported)
        {
            windowManager = null;
            result = OperationResult.Fail(
                ExecutionOutcomeCode.EnvironmentError,
                "Window management is not supported in this runtime.",
                ["No supported IWindowManager is available for the current platform/session."]);
            return false;
        }

        windowManager = _windowManager;
        result = null;
        return true;
    }

    private static OperationResult InvalidOptions(WindowAction action) =>
        OperationResult.Fail(ExecutionOutcomeCode.InvalidArguments, $"Invalid options for window action '{action}'.");

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
