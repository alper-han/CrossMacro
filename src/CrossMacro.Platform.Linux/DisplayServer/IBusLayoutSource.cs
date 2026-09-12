
namespace CrossMacro.Platform.Linux.DisplayServer;

/// <summary>
/// Detects the active keyboard layout via IBus input method daemon.
/// Works on both X11 and Wayland when IBus is the active input method.
/// </summary>
public static class IBusLayoutSource
{
    public static string? DetectLayout()
    {
        try
        {
            // Command: ibus engine
            // Output: xkb:us::eng or xkb:tr::tur
            var output = ProcessHelper.ExecuteCommand("ibus", "engine");
            return ParseEngineOutput(output);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "Error detecting IBus layout");
            return null;
        }
    }

    /// <summary>
    /// Parses the stable language component from an IBus engine identifier.
    /// </summary>
    internal static string? ParseEngineOutput(string? output)
    {
        if (string.IsNullOrWhiteSpace(output) ||
            !output.StartsWith("xkb:", StringComparison.Ordinal))
        {
            return null;
        }

        var parts = output.Split(':');
        return parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1])
            ? parts[1]
            : null;
    }
}
