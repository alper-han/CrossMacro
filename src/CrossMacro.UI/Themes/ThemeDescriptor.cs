namespace CrossMacro.UI.Themes;

public sealed record ThemeDescriptor(
    string Name,
    ThemePalette Palette,
    ThemeSourceKind SourceKind = ThemeSourceKind.BuiltIn,
    string? SourcePath = null);
