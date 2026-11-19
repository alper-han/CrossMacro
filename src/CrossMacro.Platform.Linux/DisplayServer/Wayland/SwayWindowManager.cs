
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

/// <summary>
/// Window manager implementation using Sway's binary IPC (i3-ipc) socket protocol.
/// </summary>
public sealed class SwayWindowManager(ISwayIpcClient ipcClient) : IWindowManager
{
    private const uint IpcCommand = 0;
    private const uint IpcGetWorkspaces = 1;
    private const uint IpcGetTree = 4;

    private readonly ISwayIpcClient _ipcClient = ipcClient ?? throw new ArgumentNullException(nameof(ipcClient));

    public async Task<WindowInfo?> GetActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        var windows = await GetWindowsAsync(cancellationToken).ConfigureAwait(false);
        foreach (var window in windows)
        {
            if (window.IsFocused)
            {
                return window;
            }
        }

        return null;
    }

    public async Task<IReadOnlyList<WindowInfo>> GetWindowsAsync(CancellationToken cancellationToken = default)
    {
        var response = await _ipcClient.SendRequestAsync(IpcGetTree, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(response))
        {
            return [];
        }

        try
        {
            var rootNode = JsonSerializer.Deserialize(response, SwayJsonContext.Default.SwayNodeDto);
            if (rootNode is null)
            {
                return [];
            }

            var windows = new List<WindowInfo>();
            CollectWindows(rootNode, windows, "");
            return windows;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Warning(ex, "[SwayWindowManager] Failed to parse tree response");
            return [];
        }
    }

    public Task<bool> FocusWindowByAddressAsync(string address, CancellationToken cancellationToken = default) =>
        RunOnContainerAsync(address, "focus", cancellationToken);

    public async Task<bool> FocusWindowByTitleAsync(string titleSubstring, CancellationToken cancellationToken = default)
    {
        var window = await FindWindowAsync(titleSubstring, static w => w.Title, cancellationToken).ConfigureAwait(false);
        return window is not null && await RunOnContainerAsync(window.Address, "focus", cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> FocusWindowByClassAsync(string classSubstring, CancellationToken cancellationToken = default)
    {
        var window = await FindWindowAsync(classSubstring, static w => w.Class, cancellationToken).ConfigureAwait(false);
        return window is not null && await RunOnContainerAsync(window.Address, "focus", cancellationToken).ConfigureAwait(false);
    }

    public Task<bool> CloseWindowByAddressAsync(string address, CancellationToken cancellationToken = default) =>
        RunOnContainerAsync(address, "kill", cancellationToken);

    public async Task<bool> CloseWindowByTitleAsync(string titleSubstring, CancellationToken cancellationToken = default)
    {
        var window = await FindWindowAsync(titleSubstring, static w => w.Title, cancellationToken).ConfigureAwait(false);
        return window is not null && await RunOnContainerAsync(window.Address, "kill", cancellationToken).ConfigureAwait(false);
    }

    public Task<bool> MoveActiveWindowAsync(int x, int y, CancellationToken cancellationToken = default) =>
        RunCommandAsync($"move position {x.ToString(CultureInfo.InvariantCulture)} {y.ToString(CultureInfo.InvariantCulture)}", cancellationToken);

    public Task<bool> ResizeActiveWindowAsync(int width, int height, CancellationToken cancellationToken = default) =>
        RunCommandAsync($"resize set {width.ToString(CultureInfo.InvariantCulture)} {height.ToString(CultureInfo.InvariantCulture)}", cancellationToken);

    public Task<bool> FullscreenActiveWindowAsync(CancellationToken cancellationToken = default) =>
        RunCommandAsync("fullscreen toggle", cancellationToken);

    // Sway has no maximize; fullscreen is the closest equivalent.
    public Task<bool> MaximizeActiveWindowAsync(CancellationToken cancellationToken = default) =>
        RunCommandAsync("fullscreen enable", cancellationToken);

    public Task<bool> FloatActiveWindowAsync(CancellationToken cancellationToken = default) =>
        RunCommandAsync("floating toggle", cancellationToken);

    public Task<bool> CenterActiveWindowAsync(CancellationToken cancellationToken = default) =>
        RunCommandAsync("move position center", cancellationToken);

    public async Task<string?> GetActiveWorkspaceAsync(CancellationToken cancellationToken = default)
    {
        var response = await _ipcClient.SendRequestAsync(IpcGetWorkspaces, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(response))
        {
            return null;
        }

        try
        {
            var workspaces = JsonSerializer.Deserialize(response, SwayJsonContext.Default.SwayWorkspaceDtoArray);
            if (workspaces is null)
            {
                return null;
            }

            foreach (var ws in workspaces)
            {
                if (ws.Focused)
                {
                    return ws.Name;
                }
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Warning(ex, "[SwayWindowManager] Failed to parse workspaces response");
        }
        return null;
    }

    public Task<bool> SwitchWorkspaceAsync(string workspace, CancellationToken cancellationToken = default) =>
        RunWorkspaceCommandAsync("workspace ", workspace, cancellationToken);

    public Task<bool> MoveActiveWindowToWorkspaceAsync(string workspace, CancellationToken cancellationToken = default) =>
        RunWorkspaceCommandAsync("move container to workspace ", workspace, cancellationToken);

    public async Task<bool> MoveWindowToWorkspaceByAddressAsync(string address, string workspace, CancellationToken cancellationToken = default)
    {
        if (!SwayCommandText.TryParseContainerId(address, out var id))
        {
            return false;
        }

        return await RunWorkspaceCommandAsync($"{SwayCommandText.ContainerCriteria(id)} move container to workspace ", workspace, cancellationToken).ConfigureAwait(false);
    }

    private async Task<WindowInfo?> FindWindowAsync(string substring, Func<WindowInfo, string> selector, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(substring))
        {
            return null;
        }

        var windows = await GetWindowsAsync(cancellationToken).ConfigureAwait(false);
        return windows.FirstOrDefault(w => selector(w).Contains(substring, StringComparison.OrdinalIgnoreCase));
    }

    private Task<bool> RunOnContainerAsync(string address, string command, CancellationToken cancellationToken) =>
        SwayCommandText.TryParseContainerId(address, out var id)
            ? RunCommandAsync($"{SwayCommandText.ContainerCriteria(id)} {command}", cancellationToken)
            : Task.FromResult(false);

    private Task<bool> RunWorkspaceCommandAsync(string commandPrefix, string workspace, CancellationToken cancellationToken) =>
        !string.IsNullOrWhiteSpace(workspace) && SwayCommandText.TryQuoteArgument(workspace, out var quoted)
            ? RunCommandAsync(commandPrefix + quoted, cancellationToken)
            : Task.FromResult(false);

    private async Task<bool> RunCommandAsync(string command, CancellationToken cancellationToken)
    {
        var response = await _ipcClient.SendRequestAsync(IpcCommand, command, cancellationToken).ConfigureAwait(false);
        return IsOkResponse(response);
    }

    private static void CollectWindows(SwayNodeDto node, List<WindowInfo> list, string currentWorkspace)
    {
        if (string.Equals(node.Type, "workspace", StringComparison.Ordinal) && !string.IsNullOrEmpty(node.Name))
        {
            currentWorkspace = node.Name;
        }

        if ((string.Equals(node.Type, "con", StringComparison.Ordinal) ||
            string.Equals(node.Type, "floating_con", StringComparison.Ordinal)) &&
            (!string.IsNullOrEmpty(node.Name) || !string.IsNullOrEmpty(node.AppId) || node.WindowProperties is not null))
        {
            list.Add(MapWindow(node, currentWorkspace));
        }

        if (node.Nodes is not null)
        {
            foreach (var child in node.Nodes)
            {
                CollectWindows(child, list, currentWorkspace);
            }
        }

        if (node.FloatingNodes is not null)
        {
            foreach (var child in node.FloatingNodes)
            {
                CollectWindows(child, list, currentWorkspace);
            }
        }
    }

    private static WindowInfo MapWindow(SwayNodeDto node, string workspace)
    {
        string windowClass = node.AppId ?? node.WindowProperties?.Class ?? string.Empty;

        return new WindowInfo
        {
            Address = node.Id.ToString(CultureInfo.InvariantCulture),
            Title = node.Name ?? string.Empty,
            Class = windowClass,
            Pid = node.Pid ?? 0,
            ProcessName = Helpers.ProcessHelper.GetProcessName(node.Pid ?? 0),
            Workspace = workspace,
            IsFocused = node.Focused,
            IsFullscreen = node.FullscreenMode > 0,
            IsMaximized = false,
            IsFloating = string.Equals(node.Type, "floating_con", StringComparison.Ordinal),
            IsPinned = node.Sticky,
            IsHidden = string.Equals(workspace, "__i3_scratch", StringComparison.Ordinal),
            X = node.Rect?.X ?? 0,
            Y = node.Rect?.Y ?? 0,
            Width = node.Rect?.Width ?? 0,
            Height = node.Rect?.Height ?? 0,
        };
    }

    private static bool IsOkResponse(string? response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return false;
        }

        try
        {
            var results = JsonSerializer.Deserialize(response, SwayJsonContext.Default.SwayCommandResultDtoArray);
            if (results is not null && results.Length > 0)
            {
                foreach (var result in results)
                {
                    if (!result.Success)
                    {
                        return false;
                    }
                }

                return true;
            }
        }
        catch (JsonException ex)
        {
            Log.Warning(ex, "[SwayWindowManager] Failed to parse command reply");
        }
        return false;
    }
}
