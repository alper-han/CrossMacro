using CrossMacro.Core.Logging;
using CrossMacro.Platform.Linux.Helpers;

namespace CrossMacro.Platform.Linux.DisplayServer;

/// <summary>
/// Detects the active keyboard layout via IBus input method daemon.
/// Works on both X11 and Wayland when IBus is the active input method.
/// </summary>
public class IBusLayoutSource
{
    public string? DetectLayout()
    {
        try
        {
            // Command: ibus engine
            // Output: xkb:us::eng or xkb:tr::tur
            var output = ProcessHelper.ExecuteCommand("ibus", "engine");
            if (string.IsNullOrWhiteSpace(output)) return null;

            if (output.StartsWith("xkb:"))
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
            Log.Error(ex, "Error detecting IBus layout");
            return null;
        }
    }
}
