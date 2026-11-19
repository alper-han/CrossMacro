namespace CrossMacro.Platform.MacOS.Services.WindowManagement;

/// <summary>One entry of a <c>CGWindowListCopyWindowInfo</c> snapshot; absent dictionary keys stay <see langword="null"/>.</summary>
internal readonly record struct MacOSOnScreenWindow(int? Pid, int? Layer, int? Number, ScreenRect? Bounds, string? Name)
{
    /// <summary>Distinct owning process ids of the snapshot, in snapshot order.</summary>
    internal static IReadOnlyList<int> GetApplicationPids(IReadOnlyList<MacOSOnScreenWindow> snapshot)
    {
        var pids = new List<int>();
        var seen = new HashSet<int>();
        foreach (var window in snapshot)
        {
            if (window.Pid is int pid && pid > 0 && seen.Add(pid))
            {
                pids.Add(pid);
            }
        }

        return pids;
    }

    /// <summary>
    /// The CoreGraphics window number of the single normal-layer window of <paramref name="pid"/> whose bounds equal
    /// <paramref name="frame"/> and whose title, when CoreGraphics reports one, equals <paramref name="title"/>.
    /// Ambiguous or unnumbered matches yield <see langword="null"/>.
    /// </summary>
    internal static uint? FindWindowId(IReadOnlyList<MacOSOnScreenWindow> snapshot, int pid, string title, ScreenRect frame)
    {
        uint? match = null;
        foreach (var window in snapshot)
        {
            if (window.Pid != pid || window.Layer is not 0 || window.Bounds != frame)
            {
                continue;
            }

            if (!string.IsNullOrEmpty(window.Name) && !string.Equals(window.Name, title, StringComparison.Ordinal))
            {
                continue;
            }

            if (window.Number is not > 0 || match is not null)
            {
                return null;
            }

            match = checked((uint)window.Number.Value);
        }

        return match;
    }
}
