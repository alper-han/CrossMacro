
namespace CrossMacro.Platform.Linux.Services.Keyboard;

/// <summary>
/// Detects keyboard layout across different Linux desktop environments.
/// Priority: DE-specific (Hyprland/KDE/GNOME/Niri) > IBus > X11 > localectl
/// COSMIC does not expose a reliable native current-layout API yet; fallbacks cover it.
/// Wayfire native layout IPC exists but is not wired here; generic fallbacks apply until added.
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
        : this(LinuxEnvironmentVariables.CaptureCurrentSnapshot(), new NiriLayoutSource()) { /* Empty */ }

    public LinuxLayoutDetector(ILinuxEnvironmentVariables environmentVariables)
        : this((environmentVariables ?? throw new ArgumentNullException(nameof(environmentVariables))).CaptureSnapshot(), new NiriLayoutSource()) { /* Empty */ }

    internal LinuxLayoutDetector(NiriLayoutSource niriSource)
        : this(LinuxEnvironmentVariables.CaptureCurrentSnapshot(), niriSource) { /* Empty */ }

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

    public async Task<string?> DetectLayoutAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            string? layout = await TryDetectLayoutByEnvironmentAsync(cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(layout))
            {
                return layout;
            }

            var ibusLayout = IBusLayoutSource.DetectLayout();
            if (!string.IsNullOrWhiteSpace(ibusLayout))
            {
                return ibusLayout;
            }

            var x11Layout = DetectX11Layout();
            if (!string.IsNullOrWhiteSpace(x11Layout))
            {
                return x11Layout;
            }

            return DetectLocalectlLayout();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "[LayoutDetector] Error detecting layout");
            return "us";
        }
    }

    private async Task<string?> TryDetectLayoutByEnvironmentAsync(CancellationToken cancellationToken)
    {
        if (_isHyprland)
        {
            var hyprLayout = await DetectHyprlandLayoutAsync(cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(hyprLayout))
            {
                return hyprLayout;
            }
        }

        if (_isKde)
        {
            var kdeLayout = await DetectKdeLayoutAsync(cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(kdeLayout))
            {
                return kdeLayout;
            }
        }

        if (_isGnome)
        {
            var gnomeLayout = DetectGnomeLayout();
            if (!string.IsNullOrWhiteSpace(gnomeLayout))
            {
                return gnomeLayout;
            }
        }

        if (_isNiri)
        {
            var niriLayout = await _niriSource.DetectLayoutAsync(cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(niriLayout))
            {
                return niriLayout;
            }
        }

        return null;
    }

    private static async Task<string?> DetectKdeLayoutAsync(CancellationToken cancellationToken)
    {
        try
        {
            var session = await LinuxDbusSession.ConnectAsync(cancellationToken).ConfigureAwait(false);
            using (session)
            {
                var keyboard = session.CreateKdeKeyboardClient();
                return await TryResolveKdeLayoutAsync(
                    () => keyboard.GetLayoutAsync(),
                    () => keyboard.GetLayoutsListAsync(),
                    cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Debug("[LayoutDetector] KDE DBus failed: {Message}", ex.Message);
        }
        return null;
    }

    internal static async Task<string?> TryResolveKdeLayoutAsync(
        Func<Task<uint>> getLayoutAsync,
        Func<Task<(string shortName, string variant, string displayName)[]>> getLayoutsListAsync,
        CancellationToken cancellationToken)
    {
        try
        {
            var index = await getLayoutAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
            var layouts = await getLayoutsListAsync().WaitAsync(cancellationToken).ConfigureAwait(false);

            if (index < layouts.Length)
            {
                return layouts[index].shortName;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
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
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Debug("[LayoutDetector] GNOME gsettings failed: {Message}", ex.Message);
        }
        return null;
    }

    private static async Task<string?> DetectHyprlandLayoutAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var ipcClient = new HyprlandIpcClient();
            if (!ipcClient.IsAvailable)
            {
                return null;
            }

            var json = await ipcClient.SendCommandAsync("j/devices", cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            return TryParseHyprlandLayout(json);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Debug(ex, "[LayoutDetector] Hyprland IPC failed");
        }

        return null;
    }

    private static string? TryParseHyprlandLayout(string json)
    {
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
            .FirstOrDefault(static line => line.StartsWith("layout:", StringComparison.OrdinalIgnoreCase));
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
            .FirstOrDefault(static line => line.Trim().StartsWith("X11 Layout:", StringComparison.OrdinalIgnoreCase));
        if (layoutLine is null)
        {
            return null;
        }

        var parts = layoutLine.Split(':', StringSplitOptions.TrimEntries);
        return parts.Length > 1 ? parts[1].Split(',')[0].Trim() : null;
    }

}
