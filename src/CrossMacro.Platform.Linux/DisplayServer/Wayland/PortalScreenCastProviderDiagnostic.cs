#pragma warning disable IDE0072

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal static class PortalScreenCastProviderDiagnostic
{
    private const string ScreenCastKey = "org.freedesktop.impl.portal.ScreenCast";

    public static string Describe(
        LinuxEnvironmentSnapshot environment,
        Func<string, bool> fileExists,
        Func<string, string?> readAllText)
    {
        ArgumentNullException.ThrowIfNull(fileExists);
        ArgumentNullException.ThrowIfNull(readAllText);

        var configFiles = GetCandidateFiles(environment).Distinct(StringComparer.Ordinal).ToArray();
        var foundConfig = false;
        foreach (var path in configFiles)
        {
            if (!fileExists(path))
            {
                continue;
            }

            foundConfig = true;
            var provider = TryReadScreenCastProvider(readAllText(path));
            if (!string.IsNullOrWhiteSpace(provider))
            {
                return DescribeProvider(provider, Path.GetFileName(path));
            }
        }

        return foundConfig
            ? "Portal configuration was found, but it has no explicit ScreenCast provider override; runtime selection remains external."
            : "No explicit ScreenCast provider configuration was found; runtime portal backend selection remains external.";
    }

    private static IEnumerable<string> GetCandidateFiles(LinuxEnvironmentSnapshot environment)
    {
        var desktopNames = GetDesktopNames(environment);
        var configRoots = new List<string>();
        if (!string.IsNullOrWhiteSpace(environment.XdgConfigHome))
        {
            configRoots.Add(environment.XdgConfigHome);
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(home))
        {
            configRoots.Add(Path.Combine(home, ".config"));
        }

        configRoots.Add("/etc/xdg");
        configRoots.Add("/usr/share");

        foreach (var root in configRoots.Distinct(StringComparer.Ordinal))
        {
            foreach (var desktop in desktopNames)
            {
                yield return Path.Combine(root, "xdg-desktop-portal", $"{desktop}-portals.conf");
                yield return Path.Combine(root, $"xdg-{desktop}", "portals.conf");
            }

            yield return Path.Combine(root, "xdg-desktop-portal", "portals.conf");
            yield return Path.Combine(root, "portals.conf");
        }
    }

    private static IReadOnlyList<string> GetDesktopNames(LinuxEnvironmentSnapshot environment)
    {
        var names = new List<string>();
        var compositor = CompositorDetector.ClassifyFromEnvironment(environment, isLinux: true);
        var compositorName = compositor switch
        {
            CompositorType.HYPRLAND => "hyprland",
            CompositorType.WAYFIRE => "wayfire",
            CompositorType.NIRI => "niri",
            CompositorType.COSMIC => "cosmic",
            CompositorType.SWAY => "sway",
            CompositorType.KDE => "kde",
            CompositorType.GNOME => "gnome",
            _ => null,
        };

        if (compositorName is not null)
        {
            names.Add(compositorName);
        }

        if (!string.IsNullOrWhiteSpace(environment.CurrentDesktop))
        {
            names.AddRange(environment.CurrentDesktop
                .Split([':', ';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(NormalizeDesktopName));
        }

        return names.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static string? TryReadScreenCastProvider(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var inPreferredSection = false;
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length is 0 || line.StartsWith('#') || line.StartsWith(';'))
            {
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                inPreferredSection = line.Equals("[preferred]", StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (!inPreferredSection)
            {
                continue;
            }

            var separator = line.IndexOf('=', StringComparison.Ordinal);
            if (separator <= 0 || !line[..separator].Trim().Equals(ScreenCastKey, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var provider = line[(separator + 1)..].Trim().Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
            return string.IsNullOrWhiteSpace(provider) ? null : provider;
        }

        return null;
    }

    private static string DescribeProvider(string provider, string configFileName)
    {
        if (provider.Contains("gtk", StringComparison.OrdinalIgnoreCase))
        {
            return $"Portal ScreenCast provider override selects GTK ({configFileName}), but xdg-desktop-portal-gtk does not implement ScreenCast; choose the compositor's ScreenCast backend. The portal still owns the final runtime selection.";
        }

        return $"Portal ScreenCast provider override: {provider} ({configFileName}). The portal still owns the final runtime selection.";
    }

    private static string NormalizeDesktopName(string value) => value.Trim().ToUpperInvariant() switch
    {
        "HYPRLAND" => "hyprland",
        "WAYFIRE" => "wayfire",
        "NIRI" => "niri",
        "COSMIC" => "cosmic",
        "SWAY" => "sway",
        "KDE" => "kde",
        "GNOME" => "gnome",
        _ => value.Trim(),
    };
}
