namespace CrossMacro.UI.Themes;

internal sealed record ExternalThemeLoadResult(
    IReadOnlyList<ThemeDescriptor> Themes,
    IReadOnlyList<string> Diagnostics)
{
    public static ExternalThemeLoadResult Empty { get; } = new([], []);
}
