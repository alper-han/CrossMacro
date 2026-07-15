
namespace CrossMacro.Platform.Linux.Services.Keyboard;

/// <summary>
/// Detects keyboard layout across different Linux desktop environments.
/// Priority: DE-specific (Hyprland/KDE/GNOME/Niri) > IBus > X11 > localectl
/// TODO: COSMIC does not expose a reliable native current-layout API yet; keep using fallbacks until it does.
/// TODO: Wayfire native layout IPC exists but is not wired here yet; add it before generic fallbacks.
/// </summary>
public class LinuxLayoutDetector : ILinuxLayoutDetector
{
    private static readonly string[] GnomeSourceTupleSeparators = ["), (", "),("];

    private readonly NiriLayoutSource _niriSource;
    private readonly bool _isHyprland;
    private readonly bool _isKde;
    private readonly bool _isGnome;
    private readonly bool _isNiri;

    public LinuxLayoutDetector()
        : this(LinuxEnvironmentVariables.CaptureCurrentSnapshot(), new NiriLayoutSource())
    {
    }

    public LinuxLayoutDetector(ILinuxEnvironmentVariables environmentVariables)
        : this((environmentVariables ?? throw new ArgumentNullException(nameof(environmentVariables))).CaptureSnapshot(), new NiriLayoutSource())
    {
    }

    internal LinuxLayoutDetector(NiriLayoutSource niriSource)
        : this(LinuxEnvironmentVariables.CaptureCurrentSnapshot(), niriSource)
    {
    }

    internal LinuxLayoutDetector(LinuxEnvironmentSnapshot environment, NiriLayoutSource niriSource)
    {
        _niriSource = niriSource ?? throw new ArgumentNullException(nameof(niriSource));
        _isHyprland = !string.IsNullOrEmpty(environment.HyprlandInstanceSignature);
        var desktop = environment.CurrentDesktop?.ToUpperInvariant() ?? "";
        var session = environment.GdmSession?.ToUpperInvariant() ?? "";
        _isKde = desktop.Contains("KDE", StringComparison.Ordinal) || desktop.Contains("PLASMA", StringComparison.Ordinal);
        _isGnome = desktop.Contains("GNOME", StringComparison.Ordinal) || desktop.Contains("UNITY", StringComparison.Ordinal);
        _isNiri = desktop.Contains("NIRI", StringComparison.Ordinal) || session.Contains("NIRI", StringComparison.Ordinal) || !string.IsNullOrEmpty(environment.NiriSocket);

        if (_isHyprland)
        {
            Log.Information("[LayoutDetector] Environment: Hyprland");
        }
        else if (_isKde)
        {
            Log.Information("[LayoutDetector] Environment: KDE Plasma");
        }
        else if (_isGnome)
        {
            Log.Information("[LayoutDetector] Environment: GNOME");
        }
        else if (_isNiri)
        {
            Log.Information("[LayoutDetector] Environment: Niri");
        }
        else
        {
            Log.Information("[LayoutDetector] Environment: Generic (IBus primary)");
        }
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
                {
                    return hyprLayout;
                }
            }

            // 2. KDE DBus (IBus often not used on KDE)
            if (_isKde)
            {
                var kdeLayout = DetectKdeLayout();
                if (!string.IsNullOrWhiteSpace(kdeLayout))
                {
                    return kdeLayout;
                }
            }

            // 3. GNOME GSettings
            if (_isGnome)
            {
                var gnomeLayout = DetectGnomeLayout();
                if (!string.IsNullOrWhiteSpace(gnomeLayout))
                {
                    return gnomeLayout;
                }
            }

            // 4. Niri IPC (IBus often not used on Niri)
            if (_isNiri)
            {
                var niriLayout = _niriSource.DetectLayout();
                if (!string.IsNullOrWhiteSpace(niriLayout))
                {
                    return niriLayout;
                }
            }

            // 5. IBus (Works on GNOME, etc.)
            var ibusLayout = IBusLayoutSource.DetectLayout();
            if (!string.IsNullOrWhiteSpace(ibusLayout))
            {
                return ibusLayout;
            }

            // 6. X11/XWayland fallback
            var x11Layout = DetectX11Layout();
            if (!string.IsNullOrWhiteSpace(x11Layout))
            {
                return x11Layout;
            }

            // 7. System default
            return DetectLocalectlLayout();
        }
        catch (Exception ex)
        {
            Log.LogError(ex, "[LayoutDetector] Error detecting layout");
            return "us";
        }
    }

    private static string? DetectKdeLayout()
    {
        try
        {
            using var session = LinuxDbusSession.ConnectAsync().GetAwaiter().GetResult();
            var keyboard = session.CreateKdeKeyboardClient();
            return TryResolveKdeLayout(
                () => keyboard.GetLayoutAsync().GetAwaiter().GetResult(),
                () => keyboard.GetLayoutsListAsync().GetAwaiter().GetResult());
        }
        catch (Exception ex)
        {
            Log.Debug("[LayoutDetector] KDE DBus failed: {Message}", ex.Message);
        }
        return null;
    }

    internal static string? TryResolveKdeLayout(
        Func<uint> getLayout,
        Func<(string shortName, string variant, string displayName)[]> getLayoutsList)
    {
        try
        {
            var index = getLayout();
            var layouts = getLayoutsList();

            if (index < layouts.Length)
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

    private static string? DetectGnomeLayout()
    {
        try
        {
            var currentOutput = ProcessHelper.ExecuteCommand("gsettings", "get org.gnome.desktop.input-sources current")?.Trim() ?? "";
            var currentIndexStr = currentOutput.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
            if (!uint.TryParse(currentIndexStr, out var index))
            {
                index = 0;
            }

            var sourcesOutput = ProcessHelper.ExecuteCommand("gsettings", "get org.gnome.desktop.input-sources sources")?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(sourcesOutput) || string.Equals(sourcesOutput, "@as []", StringComparison.Ordinal))
            {
                return null;
            }

            var content = sourcesOutput.Trim('[', ']');
            var tuples = content.Split(GnomeSourceTupleSeparators, StringSplitOptions.RemoveEmptyEntries);

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

    private static string? DetectHyprlandLayout()
    {
        try
        {
            using var ipcClient = new HyprlandIpcClient();
            if (!ipcClient.IsAvailable)
            {
                return null;
            }

            var json = ipcClient.SendCommandAsync("j/devices", CancellationToken.None).GetAwaiter().GetResult();
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

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

    private static string? DetectX11Layout()
    {
        var output = ProcessHelper.ExecuteCommand("setxkbmap", "-query");
        if (string.IsNullOrWhiteSpace(output))
        {
            return null;
        }

        var layoutLine = output.Split('\n')
            .FirstOrDefault(line => line.StartsWith("layout:", StringComparison.OrdinalIgnoreCase));
        if (layoutLine is null)
        {
            return null;
        }

        var parts = layoutLine.Split(':', StringSplitOptions.TrimEntries);
        return parts.Length > 1 ? parts[1].Split(',')[0].Trim() : null;
    }

    private static string? DetectLocalectlLayout()
    {
        var output = ProcessHelper.ExecuteCommand("localectl", "status");
        if (string.IsNullOrWhiteSpace(output))
        {
            return null;
        }

        var layoutLine = output.Split('\n')
            .FirstOrDefault(line => line.Trim().StartsWith("X11 Layout:", StringComparison.OrdinalIgnoreCase));
        if (layoutLine is null)
        {
            return null;
        }

        var parts = layoutLine.Split(':', StringSplitOptions.TrimEntries);
        return parts.Length > 1 ? parts[1].Split(',')[0].Trim() : null;
    }

}
