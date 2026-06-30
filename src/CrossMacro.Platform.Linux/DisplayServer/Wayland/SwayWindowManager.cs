using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Logging;
using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

/// <summary>
/// Window manager implementation using Sway's binary IPC (i3-ipc) socket protocol.
/// </summary>
public sealed class SwayWindowManager : IWindowManager
{
    private const uint IpcCommand = 0;
    private const uint IpcGetWorkspaces = 1;
    private const uint IpcGetTree = 4;

    private readonly ISwayIpcClient _ipcClient;

    public SwayWindowManager(ISwayIpcClient ipcClient)
    {
        _ipcClient = ipcClient ?? throw new ArgumentNullException(nameof(ipcClient));
    }

    public async Task<WindowInfo?> GetActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        var response = await _ipcClient.SendRequestAsync(IpcGetTree, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(response))
            return null;

        try
        {
            var rootNode = JsonSerializer.Deserialize(response, SwayJsonContext.Default.SwayNodeDto);
            if (rootNode == null)
                return null;

            var focusedNode = FindFocusedNode(rootNode);
            return focusedNode != null ? MapWindow(focusedNode, "") : null;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[SwayWindowManager] Failed to parse tree response");
            return null;
        }
    }

    public async Task<IReadOnlyList<WindowInfo>> GetWindowsAsync(CancellationToken cancellationToken = default)
    {
        var response = await _ipcClient.SendRequestAsync(IpcGetTree, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(response))
            return [];

        try
        {
            var rootNode = JsonSerializer.Deserialize(response, SwayJsonContext.Default.SwayNodeDto);
            if (rootNode == null)
                return [];

            var windows = new List<WindowInfo>();
            CollectWindows(rootNode, windows, "");
            return windows;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[SwayWindowManager] Failed to parse tree response for clients");
            return [];
        }
    }

    public async Task<bool> FocusWindowByAddressAsync(string address, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(address)) return false;
        var response = await _ipcClient.SendRequestAsync(IpcCommand, $"[con_id={address}] focus", cancellationToken).ConfigureAwait(false);
        return IsOkResponse(response);
    }

    public async Task<bool> FocusWindowByTitleAsync(string titleSubstring, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(titleSubstring)) return false;
        string escaped = titleSubstring.Replace("\"", "\\\"");
        var response = await _ipcClient.SendRequestAsync(IpcCommand, $"[title=\"{escaped}\"] focus", cancellationToken).ConfigureAwait(false);
        return IsOkResponse(response);
    }

    public async Task<bool> FocusWindowByClassAsync(string classSubstring, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(classSubstring)) return false;
        string escaped = classSubstring.Replace("\"", "\\\"");
        // Sway app_id roughly corresponds to Wayland class. XWayland windows use class=
        var response = await _ipcClient.SendRequestAsync(IpcCommand, $"[app_id=\"{escaped}\"] focus, [class=\"{escaped}\"] focus", cancellationToken).ConfigureAwait(false);
        return IsOkResponse(response);
    }

    public async Task<bool> CloseWindowByAddressAsync(string address, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(address)) return false;
        var response = await _ipcClient.SendRequestAsync(IpcCommand, $"[con_id={address}] kill", cancellationToken).ConfigureAwait(false);
        return IsOkResponse(response);
    }

    public async Task<bool> CloseWindowByTitleAsync(string titleSubstring, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(titleSubstring)) return false;
        string escaped = titleSubstring.Replace("\"", "\\\"");
        var response = await _ipcClient.SendRequestAsync(IpcCommand, $"[title=\"{escaped}\"] kill", cancellationToken).ConfigureAwait(false);
        return IsOkResponse(response);
    }

    public async Task<bool> MoveActiveWindowAsync(int x, int y, CancellationToken cancellationToken = default)
    {
        var response = await _ipcClient.SendRequestAsync(IpcCommand, $"move position {x} {y}", cancellationToken).ConfigureAwait(false);
        return IsOkResponse(response);
    }

    public async Task<bool> ResizeActiveWindowAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        var response = await _ipcClient.SendRequestAsync(IpcCommand, $"resize set {width} {height}", cancellationToken).ConfigureAwait(false);
        return IsOkResponse(response);
    }

    public async Task<bool> FullscreenActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        var response = await _ipcClient.SendRequestAsync(IpcCommand, "fullscreen toggle", cancellationToken).ConfigureAwait(false);
        return IsOkResponse(response);
    }

    public async Task<bool> MaximizeActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        // Sway uses fullscreen or floating for "maximized". We will map this to fullscreen enable.
        var response = await _ipcClient.SendRequestAsync(IpcCommand, "fullscreen enable", cancellationToken).ConfigureAwait(false);
        return IsOkResponse(response);
    }

    public async Task<bool> FloatActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        var response = await _ipcClient.SendRequestAsync(IpcCommand, "floating toggle", cancellationToken).ConfigureAwait(false);
        return IsOkResponse(response);
    }

    public async Task<bool> CenterActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        var response = await _ipcClient.SendRequestAsync(IpcCommand, "move position center", cancellationToken).ConfigureAwait(false);
        return IsOkResponse(response);
    }

    public async Task<string?> GetActiveWorkspaceAsync(CancellationToken cancellationToken = default)
    {
        var response = await _ipcClient.SendRequestAsync(IpcGetWorkspaces, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(response)) return null;

        try
        {
            var workspaces = JsonSerializer.Deserialize(response, SwayJsonContext.Default.SwayWorkspaceDtoArray);
            if (workspaces == null) return null;

            foreach (var ws in workspaces)
            {
                if (ws.Focused) return ws.Name;
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[SwayWindowManager] Failed to parse workspaces response");
        }
        return null;
    }

    public async Task<bool> SwitchWorkspaceAsync(string workspace, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workspace)) return false;
        var response = await _ipcClient.SendRequestAsync(IpcCommand, $"workspace {workspace}", cancellationToken).ConfigureAwait(false);
        return IsOkResponse(response);
    }

    public async Task<bool> MoveActiveWindowToWorkspaceAsync(string workspace, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workspace)) return false;
        var response = await _ipcClient.SendRequestAsync(IpcCommand, $"move container to workspace {workspace}", cancellationToken).ConfigureAwait(false);
        return IsOkResponse(response);
    }

    public async Task<bool> MoveWindowToWorkspaceByAddressAsync(string address, string workspace, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(address) || string.IsNullOrWhiteSpace(workspace)) return false;
        var response = await _ipcClient.SendRequestAsync(IpcCommand, $"[con_id={address}] move container to workspace {workspace}", cancellationToken).ConfigureAwait(false);
        return IsOkResponse(response);
    }

    private static SwayNodeDto? FindFocusedNode(SwayNodeDto node)
    {
        if (node.Focused && (node.Type == "con" || node.Type == "floating_con"))
        {
            return node;
        }

        if (node.Nodes != null)
        {
            foreach (var child in node.Nodes)
            {
                var found = FindFocusedNode(child);
                if (found != null) return found;
            }
        }

        if (node.FloatingNodes != null)
        {
            foreach (var child in node.FloatingNodes)
            {
                var found = FindFocusedNode(child);
                if (found != null) return found;
            }
        }

        return null;
    }

    private static void CollectWindows(SwayNodeDto node, List<WindowInfo> list, string currentWorkspace)
    {
        if (node.Type == "workspace" && !string.IsNullOrEmpty(node.Name))
        {
            currentWorkspace = node.Name;
        }

        if (node.Type == "con" || node.Type == "floating_con")
        {
            if (!string.IsNullOrEmpty(node.Name) || !string.IsNullOrEmpty(node.AppId) || node.WindowProperties != null)
            {
                list.Add(MapWindow(node, currentWorkspace));
            }
        }

        if (node.Nodes != null)
        {
            foreach (var child in node.Nodes)
            {
                CollectWindows(child, list, currentWorkspace);
            }
        }

        if (node.FloatingNodes != null)
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
            Address = node.Id.ToString(),
            Title = node.Name ?? string.Empty,
            Class = windowClass,
            Pid = node.Pid ?? 0,
            Workspace = workspace,
            IsFocused = node.Focused,
            IsFullscreen = node.FullscreenMode > 0,
            IsMaximized = false,
            IsFloating = node.Type == "floating_con",
            IsPinned = node.Sticky,
            IsHidden = workspace == "__i3_scratch",
            X = node.Rect?.X ?? 0,
            Y = node.Rect?.Y ?? 0,
            Width = node.Rect?.Width ?? 0,
            Height = node.Rect?.Height ?? 0
        };
    }

    private static bool IsOkResponse(string? response)
    {
        if (string.IsNullOrWhiteSpace(response)) return false;
        try
        {
            var results = JsonSerializer.Deserialize(response, SwayJsonContext.Default.SwayCommandResultDtoArray);
            if (results != null && results.Length > 0)
            {
                // Return true if the first command in the chain succeeded
                return results[0].Success;
            }
        }
        catch
        {
            // Ignore parse errors, fallback to false
        }
        return false;
    }
}
