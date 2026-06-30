using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Logging;
using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

/// <summary>
/// Window manager implementation using Niri IPC socket commands.
/// </summary>
internal sealed class NiriWindowManager : IWindowManager
{
    private readonly INiriIpcClient _ipcClient;

    public NiriWindowManager(INiriIpcClient ipcClient)
    {
        _ipcClient = ipcClient ?? throw new ArgumentNullException(nameof(ipcClient));
    }

    public async Task<WindowInfo?> GetActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        var response = await _ipcClient.SendRequestAsync("\"FocusedWindow\"", cancellationToken).ConfigureAwait(false);
        if (response == null) return null;

        try
        {
            var dto = JsonSerializer.Deserialize(response, NiriJsonContext.Default.NiriResponseNiriFocusedWindowData);
            if (dto?.Ok?.FocusedWindow == null) return null;

            var workspaces = await GetWorkspacesMapAsync(cancellationToken);
            var outputs = await GetOutputsMapAsync(cancellationToken);

            return MapWindow(dto.Ok.FocusedWindow, workspaces, outputs);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[NiriWindowManager] Failed to parse FocusedWindow response");
            return null;
        }
    }

    public async Task<IReadOnlyList<WindowInfo>> GetWindowsAsync(CancellationToken cancellationToken = default)
    {
        var response = await _ipcClient.SendRequestAsync("\"Windows\"", cancellationToken).ConfigureAwait(false);
        if (response == null) return Array.Empty<WindowInfo>();

        try
        {
            var dto = JsonSerializer.Deserialize(response, NiriJsonContext.Default.NiriResponseNiriWindowsData);
            if (dto?.Ok?.Windows == null) return Array.Empty<WindowInfo>();

            var workspaces = await GetWorkspacesMapAsync(cancellationToken);
            var outputs = await GetOutputsMapAsync(cancellationToken);

            return dto.Ok.Windows.Select(w => MapWindow(w, workspaces, outputs)).ToArray();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[NiriWindowManager] Failed to parse Windows response");
            return Array.Empty<WindowInfo>();
        }
    }

    private async Task<Dictionary<ulong, NiriWorkspaceDto>> GetWorkspacesMapAsync(CancellationToken cancellationToken)
    {
        var map = new Dictionary<ulong, NiriWorkspaceDto>();
        var resp = await _ipcClient.SendRequestAsync("\"Workspaces\"", cancellationToken).ConfigureAwait(false);
        if (resp != null)
        {
            var data = JsonSerializer.Deserialize(resp, NiriJsonContext.Default.NiriResponseNiriWorkspacesData);
            if (data?.Ok?.Workspaces != null)
            {
                foreach (var w in data.Ok.Workspaces) map[w.Id] = w;
            }
        }
        return map;
    }

    private async Task<Dictionary<string, NiriOutputDto>> GetOutputsMapAsync(CancellationToken cancellationToken)
    {
        var map = new Dictionary<string, NiriOutputDto>();
        var resp = await _ipcClient.SendRequestAsync("\"Outputs\"", cancellationToken).ConfigureAwait(false);
        if (resp != null)
        {
            var data = JsonSerializer.Deserialize(resp, NiriJsonContext.Default.NiriResponseNiriOutputsData);
            if (data?.Ok?.Outputs != null)
            {
                foreach (var kvp in data.Ok.Outputs) map[kvp.Key] = kvp.Value;
            }
        }
        return map;
    }

    public async Task<bool> FocusWindowByAddressAsync(string address, CancellationToken cancellationToken = default)
    {
        if (!ulong.TryParse(address, out var id)) return false;
        return await SendActionAsync($@"{{""FocusWindow"": {{""id"": {id}}}}}", cancellationToken);
    }

    public async Task<bool> FocusWindowByTitleAsync(string titleSubstring, CancellationToken cancellationToken = default)
    {
        var win = (await GetWindowsAsync(cancellationToken)).FirstOrDefault(w => w.Title.Contains(titleSubstring, StringComparison.OrdinalIgnoreCase));
        if (win != null && ulong.TryParse(win.Address, out var id))
        {
            return await SendActionAsync($@"{{""FocusWindow"": {{""id"": {id}}}}}", cancellationToken);
        }
        return false;
    }

    public async Task<bool> FocusWindowByClassAsync(string classSubstring, CancellationToken cancellationToken = default)
    {
        var win = (await GetWindowsAsync(cancellationToken)).FirstOrDefault(w => w.Class.Contains(classSubstring, StringComparison.OrdinalIgnoreCase));
        if (win != null && ulong.TryParse(win.Address, out var id))
        {
            return await SendActionAsync($@"{{""FocusWindow"": {{""id"": {id}}}}}", cancellationToken);
        }
        return false;
    }

    public async Task<bool> CloseWindowByAddressAsync(string address, CancellationToken cancellationToken = default)
    {
        if (!ulong.TryParse(address, out var id)) return false;
        return await SendActionAsync($@"{{""CloseWindow"": {{""id"": {id}}}}}", cancellationToken);
    }

    public async Task<bool> CloseWindowByTitleAsync(string titleSubstring, CancellationToken cancellationToken = default)
    {
        var win = (await GetWindowsAsync(cancellationToken)).FirstOrDefault(w => w.Title.Contains(titleSubstring, StringComparison.OrdinalIgnoreCase));
        if (win != null && ulong.TryParse(win.Address, out var id))
        {
            return await SendActionAsync($@"{{""CloseWindow"": {{""id"": {id}}}}}", cancellationToken);
        }
        return false;
    }

    public async Task<bool> MoveActiveWindowAsync(int x, int y, CancellationToken cancellationToken = default)
    {
        var outputsResp = await _ipcClient.SendRequestAsync("\"Outputs\"", cancellationToken).ConfigureAwait(false);
        if (outputsResp != null)
        {
            try
            {
                var outputsData = JsonSerializer.Deserialize(outputsResp, NiriJsonContext.Default.NiriResponseNiriOutputsData);
                if (outputsData?.Ok?.Outputs != null)
                {
                    NiriOutputDto? targetOutput = null;
                    foreach (var kvp in outputsData.Ok.Outputs)
                    {
                        var logical = kvp.Value.Logical;
                        if (logical != null && 
                            x >= logical.X && x < logical.X + logical.Width &&
                            y >= logical.Y && y < logical.Y + logical.Height)
                        {
                            targetOutput = kvp.Value;
                            break;
                        }
                    }

                    if (targetOutput?.Logical != null && !string.IsNullOrEmpty(targetOutput.Name))
                    {
                        var win = await GetActiveWindowAsync(cancellationToken).ConfigureAwait(false);
                        if (win != null)
                        {
                            var workspacesResp = await _ipcClient.SendRequestAsync("\"Workspaces\"", cancellationToken).ConfigureAwait(false);
                            if (workspacesResp != null)
                            {
                                var wsData = JsonSerializer.Deserialize(workspacesResp, NiriJsonContext.Default.NiriResponseNiriWorkspacesData);
                                var winWsIdStr = win.Workspace;
                                var winWs = wsData?.Ok?.Workspaces?.FirstOrDefault(w => w.Id.ToString() == winWsIdStr || w.Name == winWsIdStr);

                                if (winWs != null && winWs.Output != targetOutput.Name)
                                {
                                    var targetWs = wsData?.Ok?.Workspaces?.FirstOrDefault(w => w.Output == targetOutput.Name && w.IsActive);
                                    if (targetWs != null)
                                    {
                                        await SendActionAsync($@"{{""MoveWindowToWorkspace"": {{""id"": null, ""reference"": {{""Id"": {targetWs.Id}}}, ""focus"": false}}}}", cancellationToken).ConfigureAwait(false);
                                    }
                                }
                            }
                        }

                        x -= targetOutput.Logical.X;
                        y -= targetOutput.Logical.Y;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[NiriWindowManager] Failed to map global coordinates to relative");
            }
        }

        return await SendActionAsync($@"{{""MoveFloatingWindow"": {{""id"": null, ""x"": {{""SetFixed"": {x}.0}}, ""y"": {{""SetFixed"": {y}.0}}}}}}", cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> ResizeActiveWindowAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        bool w = await SendActionAsync($@"{{""SetWindowWidth"": {{""id"": null, ""change"": {{""SetFixed"": {width}}}}}}}", cancellationToken);
        bool h = await SendActionAsync($@"{{""SetWindowHeight"": {{""id"": null, ""change"": {{""SetFixed"": {height}}}}}}}", cancellationToken);
        return w && h;
    }

    public async Task<bool> FullscreenActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        return await SendActionAsync(@"{""FullscreenWindow"": {""id"": null}}", cancellationToken);
    }

    public async Task<bool> MaximizeActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        return await SendActionAsync(@"{""MaximizeWindowToEdges"": {""id"": null}}", cancellationToken);
    }

    public async Task<bool> FloatActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        var win = await GetActiveWindowAsync(cancellationToken);
        if (win == null) return false;
        var action = win.IsFloating ? "MoveWindowToTiling" : "MoveWindowToFloating";
        return await SendActionAsync($@"{{""{action}"": {{""id"": null}}}}", cancellationToken);
    }

    public async Task<bool> CenterActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        var win = await GetActiveWindowAsync(cancellationToken).ConfigureAwait(false);
        if (win == null) return false;

        var workspacesResp = await _ipcClient.SendRequestAsync("\"Workspaces\"", cancellationToken).ConfigureAwait(false);
        if (workspacesResp == null) return false;

        var outputsResp = await _ipcClient.SendRequestAsync("\"Outputs\"", cancellationToken).ConfigureAwait(false);
        if (outputsResp == null) return false;

        try
        {
            var workspacesData = JsonSerializer.Deserialize(workspacesResp, NiriJsonContext.Default.NiriResponseNiriWorkspacesData);
            var wsIdStr = win.Workspace;
            var ws = workspacesData?.Ok?.Workspaces?.FirstOrDefault(w => w.Id.ToString() == wsIdStr || w.Name == wsIdStr);
            if (ws == null || string.IsNullOrEmpty(ws.Output)) return false;

            var outputsData = JsonSerializer.Deserialize(outputsResp, NiriJsonContext.Default.NiriResponseNiriOutputsData);
            if (outputsData?.Ok?.Outputs == null || !outputsData.Ok.Outputs.TryGetValue(ws.Output, out var output) || output.Logical == null) 
                return false;

            int targetX = output.Logical.X + (output.Logical.Width - win.Width) / 2;
            int targetY = output.Logical.Y + (output.Logical.Height - win.Height) / 2;

            return await MoveActiveWindowAsync(targetX, targetY, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[NiriWindowManager] Failed to calculate center position");
            return false;
        }
    }

    public async Task<string?> GetActiveWorkspaceAsync(CancellationToken cancellationToken = default)
    {
        var response = await _ipcClient.SendRequestAsync("\"Workspaces\"", cancellationToken).ConfigureAwait(false);
        if (response == null) return null;

        try
        {
            var dto = JsonSerializer.Deserialize(response, NiriJsonContext.Default.NiriResponseNiriWorkspacesData);
            var active = dto?.Ok?.Workspaces?.FirstOrDefault(w => w.IsFocused);
            return active?.Id.ToString() ?? active?.Name;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[NiriWindowManager] Failed to parse Workspaces response");
            return null;
        }
    }

    public async Task<bool> SwitchWorkspaceAsync(string workspace, CancellationToken cancellationToken = default)
    {
        if (ulong.TryParse(workspace, out var id))
        {
            return await SendActionAsync($@"{{""FocusWorkspace"": {{""reference"": {{""Id"": {id}}}}}}}", cancellationToken);
        }
        return await SendActionAsync($@"{{""FocusWorkspace"": {{""reference"": {{""Name"": ""{workspace}""}}}}}}", cancellationToken);
    }

    public async Task<bool> MoveActiveWindowToWorkspaceAsync(string workspace, CancellationToken cancellationToken = default)
    {
        if (ulong.TryParse(workspace, out var id))
        {
            return await SendActionAsync($@"{{""MoveWindowToWorkspace"": {{""id"": null, ""reference"": {{""Id"": {id}}}, ""focus"": false}}}}", cancellationToken);
        }
        return await SendActionAsync($@"{{""MoveWindowToWorkspace"": {{""id"": null, ""reference"": {{""Name"": ""{workspace}""}}, ""focus"": false}}}}", cancellationToken);
    }

    public async Task<bool> MoveWindowToWorkspaceByAddressAsync(string address, string workspace, CancellationToken cancellationToken = default)
    {
        if (!ulong.TryParse(address, out var winId)) return false;

        if (ulong.TryParse(workspace, out var wsId))
        {
            return await SendActionAsync($@"{{""MoveWindowToWorkspace"": {{""id"": {winId}, ""reference"": {{""Id"": {wsId}}}, ""focus"": false}}}}", cancellationToken);
        }
        return await SendActionAsync($@"{{""MoveWindowToWorkspace"": {{""id"": {winId}, ""reference"": {{""Name"": ""{workspace}""}}, ""focus"": false}}}}", cancellationToken);
    }

    private async Task<bool> SendActionAsync(string actionJson, CancellationToken cancellationToken)
    {
        var payload = $@"{{""Action"": {actionJson}}}";
        var response = await _ipcClient.SendRequestAsync(payload, cancellationToken).ConfigureAwait(false);
        
        return response != null && response.Contains("\"Ok\"");
    }

    private static WindowInfo MapWindow(NiriWindowDto dto, Dictionary<ulong, NiriWorkspaceDto> workspaces, Dictionary<string, NiriOutputDto> outputs)
    {
        int x = 0;
        int y = 0;
        int w = 0;
        int h = 0;

        if (dto.Layout != null)
        {
            if (dto.Layout.TilePosInWorkspaceView != null && dto.Layout.TilePosInWorkspaceView.Length >= 2)
            {
                x = (int)Math.Round(dto.Layout.TilePosInWorkspaceView[0], MidpointRounding.AwayFromZero);
                y = (int)Math.Round(dto.Layout.TilePosInWorkspaceView[1], MidpointRounding.AwayFromZero);

                // Convert workspace-relative to global absolute
                if (dto.WorkspaceId.HasValue && workspaces.TryGetValue(dto.WorkspaceId.Value, out var ws) && !string.IsNullOrEmpty(ws.Output))
                {
                    if (outputs.TryGetValue(ws.Output, out var output) && output.Logical != null)
                    {
                        x += output.Logical.X;
                        y += output.Logical.Y;
                    }
                }
            }

            if (dto.Layout.WindowSize != null && dto.Layout.WindowSize.Length >= 2)
            {
                w = (int)Math.Round(dto.Layout.WindowSize[0], MidpointRounding.AwayFromZero);
                h = (int)Math.Round(dto.Layout.WindowSize[1], MidpointRounding.AwayFromZero);
            }
        }

        return new WindowInfo
        {
            Address = dto.Id.ToString() ?? string.Empty,
            Title = dto.Title ?? string.Empty,
            Class = dto.AppId ?? string.Empty,
            Pid = dto.Pid ?? -1,
            Workspace = dto.WorkspaceId?.ToString() ?? string.Empty,
            IsFocused = dto.IsFocused,
            IsFullscreen = false,
            IsMaximized = false,
            IsFloating = dto.IsFloating,
            IsPinned = false,
            IsHidden = false,
            X = x,
            Y = y,
            Width = w,
            Height = h
        };
    }
}

public sealed class NiriResponse<T>
{
    [JsonPropertyName("Ok")]
    public T? Ok { get; set; }
}

public sealed class NiriFocusedWindowData
{
    [JsonPropertyName("FocusedWindow")]
    public NiriWindowDto? FocusedWindow { get; set; }
}

public sealed class NiriWindowsData
{
    [JsonPropertyName("Windows")]
    public NiriWindowDto[]? Windows { get; set; }
}

public sealed class NiriWorkspacesData
{
    [JsonPropertyName("Workspaces")]
    public NiriWorkspaceDto[]? Workspaces { get; set; }
}

public sealed class NiriOutputsData
{
    [JsonPropertyName("Outputs")]
    public Dictionary<string, NiriOutputDto>? Outputs { get; set; }
}

public sealed class NiriOutputDto
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("logical")]
    public NiriLogicalGeometryDto? Logical { get; set; }
}

public sealed class NiriLogicalGeometryDto
{
    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }
}

public sealed class NiriWindowDto
{
    [JsonPropertyName("id")]
    public ulong Id { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("app_id")]
    public string? AppId { get; set; }

    [JsonPropertyName("pid")]
    public int? Pid { get; set; }

    [JsonPropertyName("workspace_id")]
    public ulong? WorkspaceId { get; set; }

    [JsonPropertyName("is_focused")]
    public bool IsFocused { get; set; }

    [JsonPropertyName("is_floating")]
    public bool IsFloating { get; set; }

    [JsonPropertyName("is_urgent")]
    public bool IsUrgent { get; set; }

    [JsonPropertyName("layout")]
    public NiriLayoutDto? Layout { get; set; }
}

public sealed class NiriLayoutDto
{
    [JsonPropertyName("pos_in_scrolling_layout")]
    public double[]? PosInScrollingLayout { get; set; }

    [JsonPropertyName("tile_size")]
    public double[]? TileSize { get; set; }

    [JsonPropertyName("window_size")]
    public double[]? WindowSize { get; set; }

    [JsonPropertyName("tile_pos_in_workspace_view")]
    public double[]? TilePosInWorkspaceView { get; set; }

    [JsonPropertyName("window_offset_in_tile")]
    public double[]? WindowOffsetInTile { get; set; }
}

public sealed class NiriWorkspaceDto
{
    [JsonPropertyName("id")]
    public ulong Id { get; set; }

    [JsonPropertyName("idx")]
    public int Idx { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("output")]
    public string? Output { get; set; }

    [JsonPropertyName("is_urgent")]
    public bool IsUrgent { get; set; }

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; }

    [JsonPropertyName("is_focused")]
    public bool IsFocused { get; set; }

    [JsonPropertyName("active_window_id")]
    public ulong? ActiveWindowId { get; set; }
}

[JsonSerializable(typeof(NiriResponse<NiriFocusedWindowData>))]
[JsonSerializable(typeof(NiriResponse<NiriWindowsData>))]
[JsonSerializable(typeof(NiriResponse<NiriWorkspacesData>))]
[JsonSerializable(typeof(NiriResponse<NiriOutputsData>))]
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal sealed partial class NiriJsonContext : JsonSerializerContext
{
}
