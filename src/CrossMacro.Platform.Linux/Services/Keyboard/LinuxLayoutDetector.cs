using System;
using System.Text.Json;
using CrossMacro.Platform.Linux.DisplayServer;
using CrossMacro.Platform.Linux.DisplayServer.Wayland;
using CrossMacro.Platform.Linux.Helpers;
using Serilog;
using Tmds.DBus;

namespace CrossMacro.Platform.Linux.Services.Keyboard;

/// <summary>
/// Detects keyboard layout across different Linux desktop environments.
/// Priority: DE-specific (Hyprland/KDE/GNOME) > IBus > X11 > localectl
/// </summary>
public class LinuxLayoutDetector : ILinuxLayoutDetector
{
    private readonly IBusLayoutSource _ibusSource = new();
    private readonly bool _isHyprland;
    private readonly bool _isKde;
    private readonly bool _isGnome;

    public LinuxLayoutDetector()
    {
        _isHyprland = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HYPRLAND_INSTANCE_SIGNATURE"));
        var desktop = Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP")?.ToUpperInvariant() ?? "";
        _isKde = desktop.Contains("KDE") || desktop.Contains("PLASMA");
        _isGnome = desktop.Contains("GNOME") || desktop.Contains("UNITY");
        
        if (_isHyprland)
            Log.Information("[LayoutDetector] Environment: Hyprland");
        else if (_isKde)
            Log.Information("[LayoutDetector] Environment: KDE Plasma");
        else if (_isGnome)
            Log.Information("[LayoutDetector] Environment: GNOME");
        else
            Log.Information("[LayoutDetector] Environment: Generic (IBus primary)");
    }

    public string? DetectLayout()
    {
        try
        {
            // 1. Hyprland IPC (IBus unreliable on Hyprland)
            if (_isHyprland)
            {
                var hyprLayout = DetectHyprlandLayout();
                if (!string.IsNullOrWhiteSpace(hyprLayout))
                    return hyprLayout;
            }

            // 2. KDE DBus (IBus often not used on KDE)
            if (_isKde)
            {
                var kdeLayout = DetectKdeLayout();
                if (!string.IsNullOrWhiteSpace(kdeLayout))
                    return kdeLayout;
            }

            // 3. GNOME GSettings
            if (_isGnome)
            {
                var gnomeLayout = DetectGnomeLayout();
                if (!string.IsNullOrWhiteSpace(gnomeLayout))
                    return gnomeLayout;
            }

            // 4. IBus (Works on GNOME, etc.)
            var ibusLayout = _ibusSource.DetectLayout();
            if (!string.IsNullOrWhiteSpace(ibusLayout))
                return ibusLayout;

            // 5. X11/XWayland fallback
            var x11Layout = DetectX11Layout();
            if (!string.IsNullOrWhiteSpace(x11Layout))
                return x11Layout;

            // 6. System default
            return DetectLocalectlLayout();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[LayoutDetector] Error detecting layout");
            return "us";
        }
    }

    private string? DetectKdeLayout()
    {
        try
        {
            using var connection = new Connection(Address.Session);
            connection.ConnectAsync().GetAwaiter().GetResult();

            var keyboard = connection.CreateProxy<IKdeKeyboard>("org.kde.keyboard", "/Layouts");
            var index = keyboard.getLayoutAsync().GetAwaiter().GetResult();
            var layouts = keyboard.getLayoutsListAsync().GetAwaiter().GetResult();
            
            if (layouts != null && index < layouts.Length)
            {
                return layouts[index].shortName;
            }
        }
        catch (Exception ex)
        {
            Log.Debug("[LayoutDetector] KDE DBus failed: {Message}", ex.Message);
        }
        return null;
    }

    private string? DetectGnomeLayout()
    {
        try
        {
            var currentOutput = ProcessHelper.ExecuteCommand("gsettings", "get org.gnome.desktop.input-sources current")?.Trim() ?? "";
            var currentIndexStr = currentOutput.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
            if (!uint.TryParse(currentIndexStr, out var index)) index = 0;

            var sourcesOutput = ProcessHelper.ExecuteCommand("gsettings", "get org.gnome.desktop.input-sources sources")?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(sourcesOutput) || sourcesOutput == "@as []") return null;

            var content = sourcesOutput.Trim('[', ']');
            var tuples = content.Split(new[] { "), (", "),(" }, StringSplitOptions.RemoveEmptyEntries);
            
            if (index < (uint)tuples.Length)
            {
                var currentTuple = tuples[index].Trim('(', ')', ' ');
                var parts = currentTuple.Split(',', StringSplitOptions.TrimEntries);
                if (parts.Length > 1)
                {
                    return parts[1].Trim('\'', '\"', ' ');
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug("[LayoutDetector] GNOME gsettings failed: {Message}", ex.Message);
        }
        return null;
    }

    private string? DetectHyprlandLayout()
    {
        try
        {
            using var ipcClient = new HyprlandIpcClient();
            if (!ipcClient.IsAvailable) return null;

            var json = ipcClient.SendCommandAsync("j/devices").GetAwaiter().GetResult();
            if (string.IsNullOrWhiteSpace(json)) return null;

            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("keyboards", out var keyboards))
            {
                foreach (var kb in keyboards.EnumerateArray())
                {
                    if (kb.TryGetProperty("active_layout_index", out _) &&
                        kb.TryGetProperty("layout", out var layout) &&
                        !string.IsNullOrWhiteSpace(layout.GetString()))
                    {
                        return layout.GetString();
                    }
                }

                foreach (var kb in keyboards.EnumerateArray())
                {
                    if (kb.TryGetProperty("layout", out var layout) && 
                        !string.IsNullOrWhiteSpace(layout.GetString()))
                    {
                        return layout.GetString();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "[LayoutDetector] Hyprland IPC failed");
        }
        return null;
    }

    private string? DetectX11Layout()
    {
        var output = ProcessHelper.ExecuteCommand("setxkbmap", "-query");
        if (string.IsNullOrWhiteSpace(output)) return null;

        foreach (var line in output.Split('\n'))
        {
            if (line.StartsWith("layout:", StringComparison.OrdinalIgnoreCase))
            {
                var parts = line.Split(':', StringSplitOptions.TrimEntries);
                if (parts.Length > 1) return parts[1].Split(',')[0].Trim();
            }
        }
        return null;
    }

    private string? DetectLocalectlLayout()
    {
        var output = ProcessHelper.ExecuteCommand("localectl", "status");
        if (string.IsNullOrWhiteSpace(output)) return null;

        foreach (var line in output.Split('\n'))
        {
            if (line.Trim().StartsWith("X11 Layout:", StringComparison.OrdinalIgnoreCase))
            {
                var parts = line.Split(':', StringSplitOptions.TrimEntries);
                if (parts.Length > 1) return parts[1].Split(',')[0].Trim();
            }
        }
        return null;
    }
}
