
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
            if (string.IsNullOrWhiteSpace(output))
            {
                return null;
            }

            if (output.StartsWith("xkb:", StringComparison.Ordinal))
            {
                var parts = output.Split(':');
                if (parts.Length > 1)
                {
                    return parts[1];
                }
            }
            return null;
        }
        catch (Exception ex)
        {
            Log.LogError(ex, "Error detecting IBus layout");
            return null;
        }
    }
}
