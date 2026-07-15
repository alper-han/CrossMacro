
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

/// <summary>
/// Window manager implementation using Hyprland IPC socket commands.
/// </summary>

public sealed class HyprlandWindowManager : IWindowManager
{
    private readonly HyprlandIpcClient _ipcClient;

    public HyprlandWindowManager(HyprlandIpcClient ipcClient)
    {
        _ipcClient = ipcClient ?? throw new ArgumentNullException(nameof(ipcClient));
    }

    public async Task<WindowInfo?> GetActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        var response = await _ipcClient.SendCommandAsync("j/activewindow", cancellationToken).ConfigureAwait(false);
        if (response is null)
            return null;

        try
        {
            var dto = JsonSerializer.Deserialize(response, HyprlandJsonContext.Default.HyprlandWindowDto);
            return dto is null ? null : MapWindow(dto, isFocused: true);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[HyprlandWindowManager] Failed to parse activewindow response");
            return null;
        }
    }

    public async Task<IReadOnlyList<WindowInfo>> GetWindowsAsync(CancellationToken cancellationToken = default)
    {
        var response = await _ipcClient.SendCommandAsync("j/clients", cancellationToken).ConfigureAwait(false);
        if (response is null)
            return [];

        try
        {
            var dtos = JsonSerializer.Deserialize(response, HyprlandJsonContext.Default.HyprlandWindowDtoArray);
            if (dtos is null)
                return [];

            var result = new List<WindowInfo>(dtos.Length);
            foreach (var dto in dtos)
                result.Add(MapWindow(dto, isFocused: dto.FocusHistoryId is 0));

            return result;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[HyprlandWindowManager] Failed to parse clients response");
            return [];
        }
    }

    public async Task<bool> FocusWindowByAddressAsync(string address, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(address))
            return false;

        var addr = NormalizeAddress(address);
        var response = await _ipcClient.SendCommandAsync($"dispatch focuswindow address:{addr}", cancellationToken).ConfigureAwait(false);
        return IsOkResponse(response);
    }

    public async Task<bool> FocusWindowByTitleAsync(string titleSubstring, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(titleSubstring))
            return false;

        var response = await _ipcClient.SendCommandAsync($"dispatch focuswindow title:{titleSubstring}", cancellationToken).ConfigureAwait(false);
        return IsOkResponse(response);
    }

    public async Task<bool> FocusWindowByClassAsync(string classSubstring, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(classSubstring))
            return false;

        var response = await _ipcClient.SendCommandAsync($"dispatch focuswindow class:{classSubstring}", cancellationToken).ConfigureAwait(false);
        return IsOkResponse(response);
    }

    public async Task<bool> CloseWindowByAddressAsync(string address, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(address))
            return false;

        var addr = NormalizeAddress(address);
        var response = await _ipcClient.SendCommandAsync($"dispatch closewindow address:{addr}", cancellationToken).ConfigureAwait(false);
        return IsOkResponse(response);
    }

    public async Task<bool> CloseWindowByTitleAsync(string titleSubstring, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(titleSubstring))
            return false;

        var response = await _ipcClient.SendCommandAsync($"dispatch closewindow title:{titleSubstring}", cancellationToken).ConfigureAwait(false);
        return IsOkResponse(response);
    }


    public async Task<bool> MoveActiveWindowAsync(int x, int y, CancellationToken cancellationToken = default)
    {
        var response = await _ipcClient.SendCommandAsync($"dispatch movewindowpixel exact {x} {y},active", cancellationToken).ConfigureAwait(false);
        return IsOkResponse(response);
    }

    public async Task<bool> ResizeActiveWindowAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        var response = await _ipcClient.SendCommandAsync($"dispatch resizewindowpixel exact {width} {height},active", cancellationToken).ConfigureAwait(false);
        return IsOkResponse(response);
    }

    public async Task<bool> FullscreenActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        var response = await _ipcClient.SendCommandAsync("dispatch fullscreen 0", cancellationToken).ConfigureAwait(false);
        return IsOkResponse(response);
    }

    public async Task<bool> MaximizeActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        var response = await _ipcClient.SendCommandAsync("dispatch fullscreen 1", cancellationToken).ConfigureAwait(false);
        return IsOkResponse(response);
    }

    public async Task<bool> FloatActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        var response = await _ipcClient.SendCommandAsync("dispatch togglefloating active", cancellationToken).ConfigureAwait(false);
        return IsOkResponse(response);
    }

    public async Task<bool> CenterActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        var response = await _ipcClient.SendCommandAsync("dispatch centerwindow", cancellationToken).ConfigureAwait(false);
        return IsOkResponse(response);
    }

    public async Task<string?> GetActiveWorkspaceAsync(CancellationToken cancellationToken = default)
    {
        var response = await _ipcClient.SendCommandAsync("j/activeworkspace", cancellationToken).ConfigureAwait(false);
        if (response is null)
            return null;

        try
        {
            var dto = JsonSerializer.Deserialize(response, HyprlandJsonContext.Default.HyprlandActiveWorkspaceDto);
            return dto?.Name;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[HyprlandWindowManager] Failed to parse activeworkspace response");
            return null;
        }
    }

    public async Task<bool> SwitchWorkspaceAsync(string workspace, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workspace))
            return false;

        var response = await _ipcClient.SendCommandAsync($"dispatch workspace {workspace}", cancellationToken).ConfigureAwait(false);
        return IsOkResponse(response);
    }

    public async Task<bool> MoveActiveWindowToWorkspaceAsync(string workspace, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workspace))
            return false;

        var response = await _ipcClient.SendCommandAsync($"dispatch movetoworkspacesilent {workspace}", cancellationToken).ConfigureAwait(false);
        return IsOkResponse(response);
    }

    public async Task<bool> MoveWindowToWorkspaceByAddressAsync(string address, string workspace, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(address) || string.IsNullOrWhiteSpace(workspace))
            return false;

        var addr = NormalizeAddress(address);
        var response = await _ipcClient.SendCommandAsync($"dispatch movetoworkspacesilent {workspace},address:{addr}", cancellationToken).ConfigureAwait(false);
        return IsOkResponse(response);
    }

    private static WindowInfo MapWindow(HyprlandWindowDto dto, bool isFocused) =>
        new()
        {
            Address = dto.Address ?? string.Empty,
            Title = dto.Title ?? string.Empty,
            Class = dto.Class ?? string.Empty,
            Pid = dto.Pid,
            ProcessName = Helpers.ProcessHelper.GetProcessName(dto.Pid),
            Workspace = dto.Workspace?.Name ?? string.Empty,
            IsFocused = isFocused,
            IsFullscreen = dto.Fullscreen is 2,
            IsMaximized = dto.Fullscreen is 1,
            IsFloating = dto.Floating,
            IsPinned = dto.Pinned,
            IsHidden = dto.Hidden, X = dto.At is not null && dto.At.Length >= 2 ? dto.At[0] : 0, Y = dto.At is not null && dto.At.Length >= 2 ? dto.At[1] : 0, Width = dto.Size is not null && dto.Size.Length >= 2 ? dto.Size[0] : 0, Height = dto.Size is not null && dto.Size.Length >= 2 ? dto.Size[1] : 0,
        };

    private static string NormalizeAddress(string address) =>
        address.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? address : "0x" + address;

    private static bool IsOkResponse(string? response)
    {
        if (response is null)
            return false;

        var trimmed = response.Trim();
        return trimmed.Equals("ok", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("ok", StringComparison.OrdinalIgnoreCase);
    }
}
