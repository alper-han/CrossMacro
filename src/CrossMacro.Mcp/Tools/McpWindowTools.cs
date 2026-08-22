namespace CrossMacro.Mcp.Tools;

public sealed class McpWindowTools(IWindowCliService windowCliService, McpToolAuthorization authorization)
{
    private const int MaximumWindowResultCount = 100;
    private const int MaximumWindowSelectorCharacters = 1_024;
    private const int DefaultWindowWaitTimeoutMs = 5_000;
    private const int MaximumWindowWaitTimeoutMs = 30_000;

    private readonly IWindowCliService _windowCliService = windowCliService;
    private readonly McpToolAuthorization _authorization = authorization;

    [McpServerTool(Name = "window.query", Title = "Query desktop windows", ReadOnly = true, Destructive = false, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpWindowQueryResult))]
    [Description("Reads the active window, a bounded window list, title/class matches, or a bounded wait result without changing desktop windows.")]
    public async Task<CallToolResult> QueryWindowsAsync(
        string mode,
        string? selectorKind = null,
        string? selectorValue = null,
        int? timeoutMs = null,
        CancellationToken cancellationToken = default)
    {
        var capability = _authorization.Require(McpCapability.WindowRead);
        if (capability is not null)
        {
            return CreateQueryResult(capability, string.Empty, [], totalCount: 0, isTruncated: false, found: null, timeoutMs: null);
        }

        ArgumentNullException.ThrowIfNull(mode);
        if (!TryCreateQueryOptions(mode, selectorKind, selectorValue, timeoutMs, out var normalizedMode, out var options, out var error))
        {
            return CreateQueryResult(error, normalizedMode, [], totalCount: 0, isTruncated: false, found: null, timeoutMs: null);
        }

        var result = await _windowCliService.ExecuteAsync(options, cancellationToken).ConfigureAwait(false);
        var outcome = McpToolOutcomeMapper.FromCliResultRedactingErrorDetails(result);
        if (!outcome.Success)
        {
            return CreateQueryResult(outcome, normalizedMode, [], totalCount: 0, isTruncated: false, found: null, options.TimeoutMs);
        }

        return result.Data switch
        {
            WindowInfoData window => CreateQueryResult(outcome, normalizedMode, [ToWindowInfo(window)], totalCount: 1, isTruncated: false, found: null, timeoutMs: null),
            WindowListData windows => CreateListResult(outcome, normalizedMode, windows),
            WindowWaitData wait => CreateWaitResult(outcome, normalizedMode, wait),
            _ => CreateQueryResult(McpToolOutcomeMapper.RuntimeError("Window query could not be read."), normalizedMode, [], totalCount: 0, isTruncated: false, found: null, options.TimeoutMs),
        };
    }

    [McpServerTool(Name = "window.control", Title = "Control desktop windows", ReadOnly = false, Destructive = true, Idempotent = false, UseStructuredContent = true, OutputSchemaType = typeof(McpWindowControlResult))]
    [Description("Focuses, closes, moves, resizes, or changes supported active-window/workspace state through the existing CrossMacro window service.")]
    public async Task<CallToolResult> ControlWindowsAsync(
        string action,
        string? selectorKind = null,
        string? selectorValue = null,
        int? x = null,
        int? y = null,
        int? width = null,
        int? height = null,
        string? workspaceName = null,
        CancellationToken cancellationToken = default)
    {
        var capability = _authorization.Require(McpCapability.WindowControl);
        if (capability is not null)
        {
            return CreateControlResult(capability, string.Empty, changed: null, workspace: null, window: null);
        }

        ArgumentNullException.ThrowIfNull(action);
        if (!TryCreateControlOptions(action, selectorKind, selectorValue, x, y, width, height, workspaceName, out var normalizedAction, out var options, out var error))
        {
            return CreateControlResult(error, normalizedAction, changed: null, workspace: null, window: null);
        }

        var result = await _windowCliService.ExecuteAsync(options, cancellationToken).ConfigureAwait(false);
        var outcome = McpToolOutcomeMapper.FromCliResultRedactingErrorDetails(result);
        if (!outcome.Success)
        {
            return CreateControlResult(outcome, normalizedAction, changed: null, workspace: null, window: null);
        }

        return result.Data switch
        {
            WindowMutationData mutation => CreateControlResult(outcome, normalizedAction, mutation.Result, workspace: null, window: null),
            WorkspaceData workspace => CreateControlResult(outcome, normalizedAction, changed: null, workspace.Workspace, window: null),
            WindowInfoData window => CreateControlResult(outcome, normalizedAction, changed: null, workspace: null, ToWindowInfo(window)),
            _ => CreateControlResult(McpToolOutcomeMapper.RuntimeError("Window control result could not be read."), normalizedAction, changed: null, workspace: null, window: null),
        };
    }

    private static bool TryCreateQueryOptions(
        string mode,
        string? selectorKind,
        string? selectorValue,
        int? timeoutMs,
        out string normalizedMode,
        out WindowCliOptions options,
        out McpToolOutcome error)
    {
        normalizedMode = mode.Trim().ToLowerInvariant();
        options = new WindowCliOptions(WindowCliAction.List);
        var action = normalizedMode switch
        {
            "active" => WindowCliAction.Active,
            "list" => WindowCliAction.List,
            "search" => WindowCliAction.Search,
            "wait" => WindowCliAction.Wait,
            _ => (WindowCliAction?)null,
        };
        if (action is null)
        {
            error = McpToolOutcomeMapper.InvalidArguments("Window query mode must be active, list, search, or wait.");
            return false;
        }

        if (action is WindowCliAction.Active or WindowCliAction.List)
        {
            if (!string.IsNullOrWhiteSpace(selectorKind) || !string.IsNullOrWhiteSpace(selectorValue))
            {
                error = McpToolOutcomeMapper.InvalidArguments("Window selectors are only supported for search and wait modes.");
                return false;
            }

            if (timeoutMs is not null)
            {
                error = McpToolOutcomeMapper.InvalidArguments("Window timeout is only supported for wait mode.");
                return false;
            }

            options = new WindowCliOptions(action.Value);
            error = McpToolOutcomeMapper.Success(string.Empty);
            return true;
        }

        if (!TryCreateSelector(selectorKind, selectorValue, out var selector, out error))
        {
            return false;
        }

        if (action is WindowCliAction.Search)
        {
            if (timeoutMs is not null)
            {
                error = McpToolOutcomeMapper.InvalidArguments("Window timeout is only supported for wait mode.");
                return false;
            }

            options = new WindowCliOptions(action.Value, selector);
            error = McpToolOutcomeMapper.Success(string.Empty);
            return true;
        }

        var effectiveTimeoutMs = timeoutMs ?? DefaultWindowWaitTimeoutMs;
        if (effectiveTimeoutMs is < 0 or > MaximumWindowWaitTimeoutMs)
        {
            error = McpToolOutcomeMapper.InvalidArguments("Window wait timeout must be between 0 and 30,000 milliseconds.");
            return false;
        }

        options = new WindowCliOptions(action.Value, selector, TimeoutMs: effectiveTimeoutMs);
        error = McpToolOutcomeMapper.Success(string.Empty);
        return true;
    }

    private static bool TryCreateControlOptions(
        string action,
        string? selectorKind,
        string? selectorValue,
        int? x,
        int? y,
        int? width,
        int? height,
        string? workspaceName,
        out string normalizedAction,
        out WindowCliOptions options,
        out McpToolOutcome error)
    {
        normalizedAction = action.Trim().ToLowerInvariant();
        options = new WindowCliOptions(WindowCliAction.Active);
        error = McpToolOutcomeMapper.Success(string.Empty);
        if (string.IsNullOrWhiteSpace(normalizedAction))
        {
            error = McpToolOutcomeMapper.InvalidArguments("Window control action is required.");
            return false;
        }

        if (workspaceName is { Length: > MaximumWindowSelectorCharacters })
        {
            error = McpToolOutcomeMapper.InvalidArguments("Workspace name exceeds the maximum allowed length.");
            return false;
        }

        if (normalizedAction is "focus" or "close")
        {
            if (!TryCreateControlSelector(selectorKind, selectorValue, normalizedAction is "close", out var selector, out error))
            {
                return false;
            }

            options = new WindowCliOptions(normalizedAction is "focus" ? WindowCliAction.Focus : WindowCliAction.Close, selector);
            return true;
        }

        if (normalizedAction is "move" or "resize")
        {
            if (selectorKind is not null || selectorValue is not null || workspaceName is not null || x is null || y is null)
            {
                error = McpToolOutcomeMapper.InvalidArguments($"Window {normalizedAction} requires x and y only.");
                return false;
            }

            if (normalizedAction is "resize" && (x <= 0 || y <= 0))
            {
                error = McpToolOutcomeMapper.InvalidArguments("Window resize width and height must be positive.");
                return false;
            }

            options = normalizedAction is "move"
                ? new WindowCliOptions(WindowCliAction.Move, X: x, Y: y)
                : new WindowCliOptions(WindowCliAction.Resize, Width: x, Height: y);
            return true;
        }

        if (normalizedAction is "workspace_switch" or "workspace_move_active" or "workspace_move_window")
        {
            if (string.IsNullOrWhiteSpace(workspaceName))
            {
                error = McpToolOutcomeMapper.InvalidArguments("Workspace control requires workspaceName.");
                return false;
            }

            if (normalizedAction is "workspace_move_window")
            {
                if (!string.Equals(selectorKind, "address", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(selectorValue))
                {
                    error = McpToolOutcomeMapper.InvalidArguments("workspace_move_window requires an address selector.");
                    return false;
                }

                options = new WindowCliOptions(WindowCliAction.WorkspaceMoveWindow, new WindowSelector(WindowSelectorKind.Address, selectorValue), WorkspaceName: workspaceName);
                return true;
            }

            if (selectorKind is not null || selectorValue is not null || x is not null || y is not null || width is not null || height is not null)
            {
                error = McpToolOutcomeMapper.InvalidArguments("Workspace control received unsupported selector or geometry fields.");
                return false;
            }

            options = normalizedAction is "workspace_switch"
                ? new WindowCliOptions(WindowCliAction.WorkspaceSwitch, WorkspaceName: workspaceName)
                : new WindowCliOptions(WindowCliAction.WorkspaceMoveActive, WorkspaceName: workspaceName);
            return true;
        }

        var flagAction = normalizedAction switch
        {
            "center" => WindowCliAction.Center,
            "maximize" => WindowCliAction.Maximize,
            "fullscreen" => WindowCliAction.Fullscreen,
            "floating" or "float" => WindowCliAction.Floating,
            _ => (WindowCliAction?)null,
        };
        if (flagAction is null || selectorKind is not null || selectorValue is not null || x is not null || y is not null || width is not null || height is not null || workspaceName is not null)
        {
            error = McpToolOutcomeMapper.InvalidArguments("Window control action or arguments are invalid.");
            return false;
        }

        options = new WindowCliOptions(flagAction.Value);
        return true;
    }

    private static bool TryCreateControlSelector(string? selectorKind, string? selectorValue, bool close, out WindowSelector selector, out McpToolOutcome error)
    {
        selector = new WindowSelector(WindowSelectorKind.Title, string.Empty);
        if (string.IsNullOrWhiteSpace(selectorKind) || string.IsNullOrWhiteSpace(selectorValue) || selectorValue.Length > MaximumWindowSelectorCharacters)
        {
            error = McpToolOutcomeMapper.InvalidArguments("Window control requires a bounded selectorKind and selectorValue.");
            return false;
        }

        var kind = selectorKind.Trim().ToLowerInvariant() switch
        {
            "address" => WindowSelectorKind.Address,
            "title" => WindowSelectorKind.Title,
            "class" when !close => WindowSelectorKind.Class,
            _ => (WindowSelectorKind?)null,
        };
        if (kind is null)
        {
            error = McpToolOutcomeMapper.InvalidArguments(close
                ? "Window close selectorKind must be address or title."
                : "Window focus selectorKind must be address, title, or class.");
            return false;
        }

        selector = new WindowSelector(kind.Value, selectorValue);
        error = McpToolOutcomeMapper.Success(string.Empty);
        return true;
    }

    private static bool TryCreateSelector(string? selectorKind, string? selectorValue, out WindowSelector selector, out McpToolOutcome error)
    {
        selector = new WindowSelector(WindowSelectorKind.Title, string.Empty);
        if (string.IsNullOrWhiteSpace(selectorKind) || string.IsNullOrWhiteSpace(selectorValue))
        {
            error = McpToolOutcomeMapper.InvalidArguments("Window search and wait require selectorKind and selectorValue.");
            return false;
        }

        if (selectorValue.Length > MaximumWindowSelectorCharacters)
        {
            error = McpToolOutcomeMapper.InvalidArguments("Window selector value exceeds the maximum allowed length.");
            return false;
        }

        var kind = selectorKind.Trim().ToLowerInvariant() switch
        {
            "title" => WindowSelectorKind.Title,
            "class" => WindowSelectorKind.Class,
            _ => (WindowSelectorKind?)null,
        };
        if (kind is null)
        {
            error = McpToolOutcomeMapper.InvalidArguments("Window selector kind must be title or class.");
            return false;
        }

        selector = new WindowSelector(kind.Value, selectorValue);
        error = McpToolOutcomeMapper.Success(string.Empty);
        return true;
    }

    private static CallToolResult CreateListResult(McpToolOutcome outcome, string mode, WindowListData windowList)
    {
        var windows = windowList.Windows.Take(MaximumWindowResultCount).Select(ToWindowInfo).ToArray();
        return CreateQueryResult(outcome, mode, windows, windowList.Count, windowList.Count > windows.Length, found: null, timeoutMs: null);
    }

    private static CallToolResult CreateWaitResult(McpToolOutcome outcome, string mode, WindowWaitData wait)
    {
        McpWindowInfo[] windows = wait.Window is null ? [] : [ToWindowInfo(wait.Window)];
        return CreateQueryResult(outcome, mode, windows, windows.Length, isTruncated: false, wait.Found, wait.TimeoutMs);
    }

    private static McpWindowInfo ToWindowInfo(WindowInfoData window) =>
        new(window.Address, window.Title, window.Class, window.Pid, window.Workspace, window.IsFocused, window.IsFullscreen, window.IsMaximized, window.IsFloating, window.IsPinned, window.IsHidden, window.X, window.Y, window.Width, window.Height);

    private static CallToolResult CreateQueryResult(McpToolOutcome outcome, string mode, IReadOnlyList<McpWindowInfo> windows, int totalCount, bool isTruncated, bool? found, int? timeoutMs) =>
        CreateResult(outcome, JsonSerializer.SerializeToElement(new McpWindowQueryResult(outcome, mode, windows, totalCount, isTruncated, found, timeoutMs), McpJsonContext.Default.McpWindowQueryResult));

    private static CallToolResult CreateControlResult(McpToolOutcome outcome, string action, bool? changed, string? workspace, McpWindowInfo? window) =>
        CreateResult(outcome, JsonSerializer.SerializeToElement(new McpWindowControlResult(outcome, action, changed, workspace, window), McpJsonContext.Default.McpWindowControlResult));

    private static CallToolResult CreateResult(McpToolOutcome outcome, JsonElement structuredContent) =>
        new()
        {
            Content = [new TextContentBlock { Text = outcome.Message }],
            StructuredContent = structuredContent,
            IsError = !outcome.Success,
        };
}
