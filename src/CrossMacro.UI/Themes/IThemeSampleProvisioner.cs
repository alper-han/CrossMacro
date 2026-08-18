namespace CrossMacro.UI.Themes;

/// <summary>
/// Writes the user-facing documentation and a copy-ready template into the external
/// themes directory so the custom-theme feature is discoverable without reading docs.
/// Implementations must be idempotent and never overwrite existing files.
/// </summary>
internal interface IThemeSampleProvisioner
{
    internal void EnsureProvisioned(string themeDirectoryPath);
}
