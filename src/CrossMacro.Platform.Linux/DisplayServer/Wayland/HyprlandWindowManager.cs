using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Logging;
using CrossMacro.Platform.Abstractions;

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
        if (response == null)
            return null;

        try
        {
            var dto = JsonSerializer.Deserialize(response, HyprlandJsonContext.Default.HyprlandWindowDto);
            return dto == null ? null : MapWindow(dto, isFocused: true);
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
        if (response == null)
            return [];

        try
        {
            var dtos = JsonSerializer.Deserialize(response, HyprlandJsonContext.Default.HyprlandWindowDtoArray);
            if (dtos == null)
                return [];

            var result = new List<WindowInfo>(dtos.Length);
            foreach (var dto in dtos)
                result.Add(MapWindow(dto, isFocused: dto.FocusHistoryId == 0));

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
        var response = await _ipcClient.SendCommandAsync($"dispatch movewindowpixel exact {x} {y}", cancellationToken).ConfigureAwait(false);
        return IsOkResponse(response);
    }

    public async Task<bool> ResizeActiveWindowAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        var response = await _ipcClient.SendCommandAsync($"dispatch resizewindowpixel exact {width} {height}", cancellationToken).ConfigureAwait(false);
        return IsOkResponse(response);
    }

    public async Task<bool> FullscreenActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        var response = await _ipcClient.SendCommandAsync("dispatch fullscreen 0", cancellationToken).ConfigureAwait(false);
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
        if (response == null)
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

        var response = await _ipcClient.SendCommandAsync($"dispatch movetoworkspace {workspace}", cancellationToken).ConfigureAwait(false);
        return IsOkResponse(response);
    }

    public async Task<bool> MoveWindowToWorkspaceByAddressAsync(string address, string workspace, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(address) || string.IsNullOrWhiteSpace(workspace))
            return false;

        var addr = NormalizeAddress(address);
        var response = await _ipcClient.SendCommandAsync($"dispatch movetoworkspace {workspace},address:{addr}", cancellationToken).ConfigureAwait(false);
        return IsOkResponse(response);
    }

    private static WindowInfo MapWindow(HyprlandWindowDto dto, bool isFocused) =>
        new()
        {
            Address = dto.Address ?? string.Empty,
            Title = dto.Title ?? string.Empty,
            Class = dto.Class ?? string.Empty,
            Pid = dto.Pid,
            Workspace = dto.Workspace?.Name ?? string.Empty,
            IsFocused = isFocused,
            IsFullscreen = dto.Fullscreen > 0,
            IsFloating = dto.Floating,
            IsPinned = dto.Pinned,
            IsHidden = dto.Hidden, X = dto.At != null && dto.At.Length >= 2 ? dto.At[0] : 0, Y = dto.At != null && dto.At.Length >= 2 ? dto.At[1] : 0, Width = dto.Size != null && dto.Size.Length >= 2 ? dto.Size[0] : 0, Height = dto.Size != null && dto.Size.Length >= 2 ? dto.Size[1] : 0
        };

    private static string NormalizeAddress(string address) =>
        address.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? address : "0x" + address;

    private static bool IsOkResponse(string? response)
    {
        if (response == null)
            return false;

        var trimmed = response.Trim();
        return trimmed.Equals("ok", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("ok", StringComparison.OrdinalIgnoreCase);
    }
}

// DTOs for Hyprland JSON responses -------------------------------------------------------

internal sealed class HyprlandWindowDto
{
    [JsonPropertyName("address")]
    public string? Address { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("class")]
    public string? Class { get; set; }

    [JsonPropertyName("pid")]
    public int Pid { get; set; }

    [JsonPropertyName("focusHistoryID")]
    public int FocusHistoryId { get; set; }

    [JsonPropertyName("fullscreen")]
    public int Fullscreen { get; set; }

    [JsonPropertyName("floating")]
    public bool Floating { get; set; }

    [JsonPropertyName("pinned")]
    public bool Pinned { get; set; }

    [JsonPropertyName("hidden")]
    public bool Hidden { get; set; }
    [JsonPropertyName("at")] public int[]? At { get; set; }
    [JsonPropertyName("size")] public int[]? Size { get; set; }

    [JsonPropertyName("workspace")]
    public HyprlandWorkspaceDto? Workspace { get; set; }
}

internal sealed class HyprlandWorkspaceDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

internal sealed class HyprlandActiveWorkspaceDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

[JsonSerializable(typeof(HyprlandWindowDto))]
[JsonSerializable(typeof(HyprlandWindowDto[]))]
[JsonSerializable(typeof(HyprlandActiveWorkspaceDto))]
internal sealed partial class HyprlandJsonContext : JsonSerializerContext
{
}
