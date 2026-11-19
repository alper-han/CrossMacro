using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.Niri;

/// <summary>
/// Window manager implementation using Niri IPC socket commands.
/// </summary>
internal sealed class NiriWindowManager(INiriIpcClient ipcClient) : IWindowManager
{
    private const string FocusedWindowRequest = "\"FocusedWindow\"";
    private const string WindowsRequest = "\"Windows\"";
    private const string WorkspacesRequest = "\"Workspaces\"";
    private const string OutputsRequest = "\"Outputs\"";

    private readonly INiriIpcClient _ipcClient = ipcClient ?? throw new ArgumentNullException(nameof(ipcClient));

    public async Task<WindowInfo?> GetActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        var focused = await GetFocusedWindowAsync(cancellationToken).ConfigureAwait(false);
        if (focused is null)
        {
            return null;
        }

        var workspaces = await GetWorkspacesAsync(cancellationToken).ConfigureAwait(false);
        var outputs = await GetOutputsAsync(cancellationToken).ConfigureAwait(false);
        return MapWindow(focused, workspaces, outputs);
    }

    public async Task<IReadOnlyList<WindowInfo>> GetWindowsAsync(CancellationToken cancellationToken = default)
    {
        var data = await QueryAsync(WindowsRequest, NiriJsonContext.Default.NiriResponseNiriWindowsData, cancellationToken).ConfigureAwait(false);
        if (data?.Windows is null)
        {
            return [];
        }

        var workspaces = await GetWorkspacesAsync(cancellationToken).ConfigureAwait(false);
        var outputs = await GetOutputsAsync(cancellationToken).ConfigureAwait(false);

        var windows = new WindowInfo[data.Windows.Count];
        for (var i = 0; i < windows.Length; i++)
        {
            windows[i] = MapWindow(data.Windows[i], workspaces, outputs);
        }

        return windows;
    }

    public Task<bool> FocusWindowByAddressAsync(string address, CancellationToken cancellationToken = default) =>
        ulong.TryParse(address, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
            ? SendActionAsync(NiriActionRequest.FocusWindow(id), cancellationToken)
            : Task.FromResult(false);

    public async Task<bool> FocusWindowByTitleAsync(string titleSubstring, CancellationToken cancellationToken = default)
    {
        var window = await FindWindowAsync(titleSubstring, static w => w.Title, cancellationToken).ConfigureAwait(false);
        return window is not null && await FocusWindowByAddressAsync(window.Address, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> FocusWindowByClassAsync(string classSubstring, CancellationToken cancellationToken = default)
    {
        var window = await FindWindowAsync(classSubstring, static w => w.Class, cancellationToken).ConfigureAwait(false);
        return window is not null && await FocusWindowByAddressAsync(window.Address, cancellationToken).ConfigureAwait(false);
    }

    public Task<bool> CloseWindowByAddressAsync(string address, CancellationToken cancellationToken = default) =>
        ulong.TryParse(address, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
            ? SendActionAsync(NiriActionRequest.CloseWindow(id), cancellationToken)
            : Task.FromResult(false);

    public async Task<bool> CloseWindowByTitleAsync(string titleSubstring, CancellationToken cancellationToken = default)
    {
        var window = await FindWindowAsync(titleSubstring, static w => w.Title, cancellationToken).ConfigureAwait(false);
        return window is not null && await CloseWindowByAddressAsync(window.Address, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> MoveActiveWindowAsync(int x, int y, CancellationToken cancellationToken = default)
    {
        var outputs = await GetOutputsAsync(cancellationToken).ConfigureAwait(false);
        var target = outputs.Values.FirstOrDefault(o =>
            o.Logical is { } logical && !string.IsNullOrEmpty(o.Name)
            && x >= logical.X && x < logical.X + logical.Width
            && y >= logical.Y && y < logical.Y + logical.Height);

        if (target is { Logical: { } targetLogical, Name: { } targetName })
        {
            await MoveActiveWindowToOutputAsync(targetName, cancellationToken).ConfigureAwait(false);
            x -= targetLogical.X;
            y -= targetLogical.Y;
        }

        return await SendActionAsync(NiriActionRequest.MoveActiveFloatingWindow(x, y), cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> ResizeActiveWindowAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        bool widthSet = await SendActionAsync(NiriActionRequest.SetActiveWindowWidth(width), cancellationToken).ConfigureAwait(false);
        bool heightSet = await SendActionAsync(NiriActionRequest.SetActiveWindowHeight(height), cancellationToken).ConfigureAwait(false);
        return widthSet && heightSet;
    }

    public Task<bool> FullscreenActiveWindowAsync(CancellationToken cancellationToken = default) =>
        SendActionAsync(NiriActionRequest.FullscreenActiveWindow(), cancellationToken);

    public Task<bool> MaximizeActiveWindowAsync(CancellationToken cancellationToken = default) =>
        SendActionAsync(NiriActionRequest.MaximizeActiveWindowToEdges(), cancellationToken);

    public async Task<bool> FloatActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        var focused = await GetFocusedWindowAsync(cancellationToken).ConfigureAwait(false);
        if (focused is null)
        {
            return false;
        }

        var action = focused.IsFloating ? NiriActionRequest.MoveActiveWindowToTiling() : NiriActionRequest.MoveActiveWindowToFloating();
        return await SendActionAsync(action, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> CenterActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        var focused = await GetFocusedWindowAsync(cancellationToken).ConfigureAwait(false);
        if (focused is null)
        {
            return false;
        }

        var workspaces = await GetWorkspacesAsync(cancellationToken).ConfigureAwait(false);
        var outputs = await GetOutputsAsync(cancellationToken).ConfigureAwait(false);
        if (focused.WorkspaceId is not { } workspaceId
            || !workspaces.TryGetValue(workspaceId, out var workspace)
            || string.IsNullOrEmpty(workspace.Output)
            || !outputs.TryGetValue(workspace.Output, out var output)
            || output.Logical is not { } logical)
        {
            return false;
        }

        var window = MapWindow(focused, workspaces, outputs);
        int targetX = logical.X + ((logical.Width - window.Width) / 2);
        int targetY = logical.Y + ((logical.Height - window.Height) / 2);

        return await MoveActiveWindowAsync(targetX, targetY, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string?> GetActiveWorkspaceAsync(CancellationToken cancellationToken = default)
    {
        var data = await QueryAsync(WorkspacesRequest, NiriJsonContext.Default.NiriResponseNiriWorkspacesData, cancellationToken).ConfigureAwait(false);
        var active = data?.Workspaces?.FirstOrDefault(static w => w.IsFocused);
        return active?.Id.ToString(CultureInfo.InvariantCulture) ?? active?.Name;
    }

    public Task<bool> SwitchWorkspaceAsync(string workspace, CancellationToken cancellationToken = default) =>
        SendActionAsync(NiriActionRequest.FocusWorkspace(workspace), cancellationToken);

    public Task<bool> MoveActiveWindowToWorkspaceAsync(string workspace, CancellationToken cancellationToken = default) =>
        SendActionAsync(NiriActionRequest.MoveWindowToWorkspace(windowId: null, workspace), cancellationToken);

    public Task<bool> MoveWindowToWorkspaceByAddressAsync(string address, string workspace, CancellationToken cancellationToken = default) =>
        ulong.TryParse(address, NumberStyles.None, CultureInfo.InvariantCulture, out var windowId)
            ? SendActionAsync(NiriActionRequest.MoveWindowToWorkspace(windowId, workspace), cancellationToken)
            : Task.FromResult(false);

    private async Task<WindowInfo?> FindWindowAsync(string substring, Func<WindowInfo, string> selector, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(substring))
        {
            return null;
        }

        var windows = await GetWindowsAsync(cancellationToken).ConfigureAwait(false);
        return windows.FirstOrDefault(w => selector(w).Contains(substring, StringComparison.OrdinalIgnoreCase));
    }

    private async Task MoveActiveWindowToOutputAsync(string outputName, CancellationToken cancellationToken)
    {
        var focused = await GetFocusedWindowAsync(cancellationToken).ConfigureAwait(false);
        if (focused?.WorkspaceId is not { } workspaceId)
        {
            return;
        }

        var workspaces = await GetWorkspacesAsync(cancellationToken).ConfigureAwait(false);
        if (!workspaces.TryGetValue(workspaceId, out var current)
            || string.Equals(current.Output, outputName, StringComparison.Ordinal))
        {
            return;
        }

        var target = workspaces.Values.FirstOrDefault(w => w.IsActive && string.Equals(w.Output, outputName, StringComparison.Ordinal));
        if (target is not null)
        {
            _ = await SendActionAsync(NiriActionRequest.MoveWindowToWorkspace(windowId: null, target.Id), cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<NiriWindowDto?> GetFocusedWindowAsync(CancellationToken cancellationToken)
    {
        var data = await QueryAsync(FocusedWindowRequest, NiriJsonContext.Default.NiriResponseNiriFocusedWindowData, cancellationToken).ConfigureAwait(false);
        return data?.FocusedWindow;
    }

    private async Task<Dictionary<ulong, NiriWorkspaceDto>> GetWorkspacesAsync(CancellationToken cancellationToken)
    {
        var data = await QueryAsync(WorkspacesRequest, NiriJsonContext.Default.NiriResponseNiriWorkspacesData, cancellationToken).ConfigureAwait(false);
        var map = new Dictionary<ulong, NiriWorkspaceDto>();
        if (data?.Workspaces is not null)
        {
            foreach (var workspace in data.Workspaces)
            {
                map[workspace.Id] = workspace;
            }
        }

        return map;
    }

    private async Task<Dictionary<string, NiriOutputDto>> GetOutputsAsync(CancellationToken cancellationToken)
    {
        var data = await QueryAsync(OutputsRequest, NiriJsonContext.Default.NiriResponseNiriOutputsData, cancellationToken).ConfigureAwait(false);
        return data?.Outputs is null
            ? new Dictionary<string, NiriOutputDto>(StringComparer.Ordinal)
            : new Dictionary<string, NiriOutputDto>(data.Outputs, StringComparer.Ordinal);
    }

    private async Task<T?> QueryAsync<T>(string request, JsonTypeInfo<NiriResponse<T>> typeInfo, CancellationToken cancellationToken)
        where T : class
    {
        var response = await _ipcClient.SendRequestAsync(request, cancellationToken).ConfigureAwait(false);
        if (response is null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(response, typeInfo)?.Ok;
        }
        catch (JsonException ex)
        {
            Log.Warning(ex, "[NiriWindowManager] Failed to parse {Request} response", request);
            return null;
        }
    }

    private async Task<bool> SendActionAsync(string actionRequest, CancellationToken cancellationToken)
    {
        var response = await _ipcClient.SendRequestAsync(actionRequest, cancellationToken).ConfigureAwait(false);
        if (response is null)
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(response);
            return document.RootElement.ValueKind is JsonValueKind.Object && document.RootElement.TryGetProperty("Ok", out _);
        }
        catch (JsonException ex)
        {
            Log.Warning(ex, "[NiriWindowManager] Failed to parse action reply");
            return false;
        }
    }

    private static WindowInfo MapWindow(NiriWindowDto dto, Dictionary<ulong, NiriWorkspaceDto> workspaces, Dictionary<string, NiriOutputDto> outputs)
    {
        int x = 0;
        int y = 0;
        int w = 0;
        int h = 0;

        if (dto.Layout is not null)
        {
            if (dto.Layout.TilePosInWorkspaceView is not null && dto.Layout.TilePosInWorkspaceView.Count >= 2)
            {
                x = (int)Math.Round(dto.Layout.TilePosInWorkspaceView[0], MidpointRounding.AwayFromZero);
                y = (int)Math.Round(dto.Layout.TilePosInWorkspaceView[1], MidpointRounding.AwayFromZero);

                // Convert workspace-relative to global absolute
                if (dto.WorkspaceId is not null && workspaces.TryGetValue(dto.WorkspaceId.Value, out var ws) && !string.IsNullOrEmpty(ws.Output) && outputs.TryGetValue(ws.Output, out var output) && output.Logical is not null)
                {
                    x += output.Logical.X;
                    y += output.Logical.Y;
                }
            }

            if (dto.Layout.WindowSize is not null && dto.Layout.WindowSize.Count >= 2)
            {
                w = (int)Math.Round(dto.Layout.WindowSize[0], MidpointRounding.AwayFromZero);
                h = (int)Math.Round(dto.Layout.WindowSize[1], MidpointRounding.AwayFromZero);
            }
        }

        return new WindowInfo
        {
            Address = dto.Id.ToString(CultureInfo.InvariantCulture),
            Title = dto.Title ?? string.Empty,
            Class = dto.AppId ?? string.Empty,
            Pid = dto.Pid ?? -1,
            ProcessName = Helpers.ProcessHelper.GetProcessName(dto.Pid ?? -1),
            Workspace = dto.WorkspaceId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            IsFocused = dto.IsFocused,
            IsFullscreen = false,
            IsMaximized = false,
            IsFloating = dto.IsFloating,
            IsPinned = false,
            IsHidden = false,
            X = x,
            Y = y,
            Width = w,
            Height = h,
        };
    }
}
