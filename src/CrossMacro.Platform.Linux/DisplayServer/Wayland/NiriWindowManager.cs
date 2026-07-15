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
        if (response is null) return null;

        try
        {
            var dto = JsonSerializer.Deserialize(response, NiriJsonContext.Default.NiriResponseNiriFocusedWindowData);
            if ((dto?.Ok?.FocusedWindow) is null) return null;

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
        if (response is null) return Array.Empty<WindowInfo>();

        try
        {
            var dto = JsonSerializer.Deserialize(response, NiriJsonContext.Default.NiriResponseNiriWindowsData);
            if ((dto?.Ok?.Windows) is null) return Array.Empty<WindowInfo>();

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
        if (resp is not null)
        {
            var data = JsonSerializer.Deserialize(resp, NiriJsonContext.Default.NiriResponseNiriWorkspacesData);
            if ((data?.Ok?.Workspaces) is not null)
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
        if (resp is not null)
        {
            var data = JsonSerializer.Deserialize(resp, NiriJsonContext.Default.NiriResponseNiriOutputsData);
            if ((data?.Ok?.Outputs) is not null)
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
        if (outputsResp is not null)
        {
            try
            {
                var outputsData = JsonSerializer.Deserialize(outputsResp, NiriJsonContext.Default.NiriResponseNiriOutputsData);
                if ((outputsData?.Ok?.Outputs) is not null)
                {
                    NiriOutputDto? targetOutput = null;
                    foreach (var kvp in outputsData.Ok.Outputs)
                    {
                        var logical = kvp.Value.Logical;
                        if (logical is not null && x >= logical.X && x < logical.X + logical.Width && y >= logical.Y && y < logical.Y + logical.Height)
                        {
                            targetOutput = kvp.Value;
                            break;
                        }
                    }

                    if ((targetOutput?.Logical) is not null && !string.IsNullOrEmpty(targetOutput.Name))
                    {
                        var win = await GetActiveWindowAsync(cancellationToken).ConfigureAwait(false);
                        if (win != null)
                        {
                            var workspacesResp = await _ipcClient.SendRequestAsync("\"Workspaces\"", cancellationToken).ConfigureAwait(false);
                            if (workspacesResp is not null)
                            {
                                var wsData = JsonSerializer.Deserialize(workspacesResp, NiriJsonContext.Default.NiriResponseNiriWorkspacesData);
                                var winWsIdStr = win.Workspace;
                                var winWs = wsData?.Ok?.Workspaces?.FirstOrDefault(w => string.Equals(w.Id.ToString(), winWsIdStr, StringComparison.Ordinal) || string.Equals(w.Name, winWsIdStr, StringComparison.Ordinal));

                                if (winWs is not null && !string.Equals(winWs.Output, targetOutput.Name, StringComparison.Ordinal))
                                {
                                    var targetWs = wsData?.Ok?.Workspaces?.FirstOrDefault(w => string.Equals(w.Output, targetOutput.Name, StringComparison.Ordinal) && w.IsActive);
                                    if (targetWs is not null)
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
        if (workspacesResp is null) return false;

        var outputsResp = await _ipcClient.SendRequestAsync("\"Outputs\"", cancellationToken).ConfigureAwait(false);
        if (outputsResp is null) return false;

        try
        {
            var workspacesData = JsonSerializer.Deserialize(workspacesResp, NiriJsonContext.Default.NiriResponseNiriWorkspacesData);
            var wsIdStr = win.Workspace;
            var ws = workspacesData?.Ok?.Workspaces?.FirstOrDefault(w => string.Equals(w.Id.ToString(), wsIdStr, StringComparison.Ordinal) || string.Equals(w.Name, wsIdStr, StringComparison.Ordinal));
            if (ws is null || string.IsNullOrEmpty(ws.Output)) return false;

            var outputsData = JsonSerializer.Deserialize(outputsResp, NiriJsonContext.Default.NiriResponseNiriOutputsData);
            if ((outputsData?.Ok?.Outputs) is null || !outputsData.Ok.Outputs.TryGetValue(ws.Output, out var output) || output.Logical is null)
                return false;

            int targetX = output.Logical.X + ((output.Logical.Width - win.Width) / 2);
            int targetY = output.Logical.Y + ((output.Logical.Height - win.Height) / 2);

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
        if (response is null) return null;

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

        return response is not null && response.Contains("\"Ok\"", StringComparison.Ordinal);
    }

    private static WindowInfo MapWindow(NiriWindowDto dto, Dictionary<ulong, NiriWorkspaceDto> workspaces, Dictionary<string, NiriOutputDto> outputs)
    {
        int x = 0;
        int y = 0;
        int w = 0;
        int h = 0;

        if (dto.Layout is not null)
        {
            if (dto.Layout.TilePosInWorkspaceView is not null && dto.Layout.TilePosInWorkspaceView.Length >= 2)
            {
                x = (int)Math.Round(dto.Layout.TilePosInWorkspaceView[0], MidpointRounding.AwayFromZero);
                y = (int)Math.Round(dto.Layout.TilePosInWorkspaceView[1], MidpointRounding.AwayFromZero);

                // Convert workspace-relative to global absolute
                if (dto.WorkspaceId.HasValue && workspaces.TryGetValue(dto.WorkspaceId.Value, out var ws) && !string.IsNullOrEmpty(ws.Output) && outputs.TryGetValue(ws.Output, out var output) && output.Logical is not null)
                {
                    x += output.Logical.X;
                    y += output.Logical.Y;
                }
            }

            if (dto.Layout.WindowSize is not null && dto.Layout.WindowSize.Length >= 2)
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
            ProcessName = Helpers.ProcessHelper.GetProcessName(dto.Pid ?? -1),
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
            Height = h,
        };
    }
}
