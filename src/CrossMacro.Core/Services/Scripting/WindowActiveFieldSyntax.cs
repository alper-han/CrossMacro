namespace CrossMacro.Core.Services.Scripting;

/// <summary>Stable script spellings for active-window fields; independent of presentation and platform APIs.</summary>
public static class WindowActiveFieldSyntax
{
    public static bool TryParse(string? token, out WindowActiveField field)
    {
        field = token?.Trim().ToUpperInvariant() switch
        {
            "TITLE" => WindowActiveField.Title,
            "CLASS" => WindowActiveField.Class,
            "ADDRESS" => WindowActiveField.Address,
            "FULLSCREEN" => WindowActiveField.Fullscreen,
            "MAXIMIZE" => WindowActiveField.Maximize,
            "FLOAT" => WindowActiveField.Floating,
            "PINNED" => WindowActiveField.Pinned,
            "HIDDEN" => WindowActiveField.Hidden,
            "GEOMETRY" => WindowActiveField.Geometry,
            _ => WindowActiveField.Unknown,
        };
        return field is not WindowActiveField.Unknown;
    }

    public static IReadOnlyList<string> Tokens { get; } = Array.AsReadOnly(
        Enum.GetValues<WindowActiveField>().Where(field => field is not WindowActiveField.Unknown).Select(Format).ToArray());

    public static string Normalize(string? token) =>
        TryParse(token, out var field) ? Format(field) : token?.Trim() ?? string.Empty;

    public static string Format(WindowActiveField field) => field switch
    {
        WindowActiveField.Title => "title",
        WindowActiveField.Class => "class",
        WindowActiveField.Address => "address",
        WindowActiveField.Fullscreen => "fullscreen",
        WindowActiveField.Maximize => "maximize",
        WindowActiveField.Floating => "float",
        WindowActiveField.Pinned => "pinned",
        WindowActiveField.Hidden => "hidden",
        WindowActiveField.Geometry => "geometry",
        WindowActiveField.Unknown => string.Empty,
        _ => throw new ArgumentOutOfRangeException(nameof(field)),
    };
}
