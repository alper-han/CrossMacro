
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.Hyprland;

/// <summary>
/// Window manager implementation using Hyprland IPC socket commands.
/// </summary>

public sealed class HyprlandWindowManager(HyprlandIpcClient ipcClient) : IWindowManager
{
    private static readonly SearchValues<char> HexDigits = SearchValues.Create("0123456789abcdefABCDEF");

    private readonly HyprlandIpcClient _ipcClient = ipcClient ?? throw new ArgumentNullException(nameof(ipcClient));

    public async Task<WindowInfo?> GetActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        var response = await _ipcClient.SendCommandAsync("j/activewindow", cancellationToken).ConfigureAwait(false);
        if (response is null)
        {
            return null;
        }

        try
        {
            var dto = JsonSerializer.Deserialize(response, HyprlandJsonContext.Default.HyprlandWindowDto);
            return dto is null ? null : MapWindow(dto, isFocused: true);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Warning(ex, "[HyprlandWindowManager] Failed to parse activewindow response");
            return null;
        }
    }

    public async Task<IReadOnlyList<WindowInfo>> GetWindowsAsync(CancellationToken cancellationToken = default)
    {
        var response = await _ipcClient.SendCommandAsync("j/clients", cancellationToken).ConfigureAwait(false);
        if (response is null)
        {
            return [];
        }

        try
        {
            var dtos = JsonSerializer.Deserialize(response, HyprlandJsonContext.Default.HyprlandWindowDtoArray);
            if (dtos is null)
            {
                return [];
            }

            var result = new List<WindowInfo>(dtos.Length);
            foreach (var dto in dtos)
            {
                result.Add(MapWindow(dto, isFocused: dto.FocusHistoryId is 0));
            }

            return result;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Warning(ex, "[HyprlandWindowManager] Failed to parse clients response");
            return [];
        }
    }

    public Task<bool> FocusWindowByAddressAsync(string address, CancellationToken cancellationToken = default) =>
        DispatchOnWindowAsync("focuswindow", address, cancellationToken);

    public async Task<bool> FocusWindowByTitleAsync(string titleSubstring, CancellationToken cancellationToken = default)
    {
        var window = await FindWindowAsync(titleSubstring, static w => w.Title, cancellationToken).ConfigureAwait(false);
        return window is not null && await DispatchOnWindowAsync("focuswindow", window.Address, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> FocusWindowByClassAsync(string classSubstring, CancellationToken cancellationToken = default)
    {
        var window = await FindWindowAsync(classSubstring, static w => w.Class, cancellationToken).ConfigureAwait(false);
        return window is not null && await DispatchOnWindowAsync("focuswindow", window.Address, cancellationToken).ConfigureAwait(false);
    }

    public Task<bool> CloseWindowByAddressAsync(string address, CancellationToken cancellationToken = default) =>
        DispatchOnWindowAsync("closewindow", address, cancellationToken);

    public async Task<bool> CloseWindowByTitleAsync(string titleSubstring, CancellationToken cancellationToken = default)
    {
        var window = await FindWindowAsync(titleSubstring, static w => w.Title, cancellationToken).ConfigureAwait(false);
        return window is not null && await DispatchOnWindowAsync("closewindow", window.Address, cancellationToken).ConfigureAwait(false);
    }

    public Task<bool> MoveActiveWindowAsync(int x, int y, CancellationToken cancellationToken = default) =>
        DispatchAsync($"movewindowpixel exact {x.ToString(CultureInfo.InvariantCulture)} {y.ToString(CultureInfo.InvariantCulture)},active", cancellationToken);

    public Task<bool> ResizeActiveWindowAsync(int width, int height, CancellationToken cancellationToken = default) =>
        DispatchAsync($"resizewindowpixel exact {width.ToString(CultureInfo.InvariantCulture)} {height.ToString(CultureInfo.InvariantCulture)},active", cancellationToken);

    public Task<bool> FullscreenActiveWindowAsync(CancellationToken cancellationToken = default) =>
        DispatchAsync("fullscreen 0", cancellationToken);

    public Task<bool> MaximizeActiveWindowAsync(CancellationToken cancellationToken = default) =>
        DispatchAsync("fullscreen 1", cancellationToken);

    public Task<bool> FloatActiveWindowAsync(CancellationToken cancellationToken = default) =>
        DispatchAsync("togglefloating active", cancellationToken);

    public Task<bool> CenterActiveWindowAsync(CancellationToken cancellationToken = default) =>
        DispatchAsync("centerwindow", cancellationToken);

    public async Task<string?> GetActiveWorkspaceAsync(CancellationToken cancellationToken = default)
    {
        var response = await _ipcClient.SendCommandAsync("j/activeworkspace", cancellationToken).ConfigureAwait(false);
        if (response is null)
        {
            return null;
        }

        try
        {
            var dto = JsonSerializer.Deserialize(response, HyprlandJsonContext.Default.HyprlandActiveWorkspaceDto);
            return dto?.Name;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Warning(ex, "[HyprlandWindowManager] Failed to parse activeworkspace response");
            return null;
        }
    }

    public Task<bool> SwitchWorkspaceAsync(string workspace, CancellationToken cancellationToken = default) =>
        IsPlainWorkspaceName(workspace)
            ? DispatchAsync($"workspace {workspace}", cancellationToken)
            : Task.FromResult(false);

    public Task<bool> MoveActiveWindowToWorkspaceAsync(string workspace, CancellationToken cancellationToken = default) =>
        IsPlainWorkspaceName(workspace)
            ? DispatchAsync($"movetoworkspacesilent {workspace}", cancellationToken)
            : Task.FromResult(false);

    public Task<bool> MoveWindowToWorkspaceByAddressAsync(string address, string workspace, CancellationToken cancellationToken = default) =>
        TryNormalizeAddress(address, out var addr) && IsPlainWorkspaceName(workspace)
            ? DispatchAsync($"movetoworkspacesilent {workspace},address:{addr}", cancellationToken)
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

    private Task<bool> DispatchOnWindowAsync(string dispatcher, string address, CancellationToken cancellationToken) =>
        TryNormalizeAddress(address, out var addr)
            ? DispatchAsync($"{dispatcher} address:{addr}", cancellationToken)
            : Task.FromResult(false);

    private async Task<bool> DispatchAsync(string dispatch, CancellationToken cancellationToken)
    {
        var response = await _ipcClient.SendCommandAsync($"dispatch {dispatch}", cancellationToken).ConfigureAwait(false);
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
            IsHidden = dto.Hidden,
            X = dto.At is not null && dto.At.Length >= 2 ? dto.At[0] : 0,
            Y = dto.At is not null && dto.At.Length >= 2 ? dto.At[1] : 0,
            Width = dto.Size is not null && dto.Size.Length >= 2 ? dto.Size[0] : 0,
            Height = dto.Size is not null && dto.Size.Length >= 2 ? dto.Size[1] : 0,
        };

    private static bool TryNormalizeAddress(string? address, out string normalized)
    {
        var digits = address.AsSpan();
        if (digits.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            digits = digits[2..];
        }

        if (digits.IsEmpty || digits.ContainsAnyExcept(HexDigits))
        {
            normalized = string.Empty;
            return false;
        }

        normalized = "0x" + digits.ToString();
        return true;
    }

    private static bool IsPlainWorkspaceName(string workspace) =>
        !string.IsNullOrWhiteSpace(workspace) && workspace.All(static c => !char.IsControl(c));

    private static bool IsOkResponse(string? response)
    {
        if (response is null)
        {
            return false;
        }

        var trimmed = response.Trim();
        return trimmed.Equals("ok", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("ok", StringComparison.OrdinalIgnoreCase);
    }
}