namespace CrossMacro.UI.ViewModels.Settings;

public sealed record ThemePreferenceOption(ThemePreferenceMode Mode, string Name, string? ThemeName = null)
{
    public bool IsSystem => Mode is ThemePreferenceMode.System;
    public ThemeAppearance? Appearance { get; init; }
    public string DisplayName { get; init; } = Name;
}
