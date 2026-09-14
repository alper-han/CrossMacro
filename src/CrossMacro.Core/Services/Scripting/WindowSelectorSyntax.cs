namespace CrossMacro.Core.Services.Scripting;

/// <summary>Canonical selector spellings and command capabilities shared by editor and runtime.</summary>
public static class WindowSelectorSyntax
{
    private static readonly IReadOnlyList<string> Search = Array.AsReadOnly<string>(["title", "class"]);
    private static readonly IReadOnlyList<string> Focus = Array.AsReadOnly<string>(["active", "title", "class", "address"]);
    private static readonly IReadOnlyList<string> Close = Array.AsReadOnly<string>(["active", "title", "address"]);

    public static WindowTargetKind ParseCanonical(string? token) => token switch
    {
        "active" => WindowTargetKind.Active,
        "title" => WindowTargetKind.Title,
        "class" => WindowTargetKind.Class,
        "address" => WindowTargetKind.Address,
        _ => WindowTargetKind.Unknown,
    };

    public static IReadOnlyList<string> GetAllowedTokens(WindowCommandMode mode) => mode switch
    {
        WindowCommandMode.Search or WindowCommandMode.Wait => Search,
        WindowCommandMode.Focus => Focus,
        WindowCommandMode.Close => Close,
        WindowCommandMode.Active or WindowCommandMode.Move or WindowCommandMode.Resize
            or WindowCommandMode.Center or WindowCommandMode.Maximize or WindowCommandMode.Fullscreen
            or WindowCommandMode.Floating or WindowCommandMode.WorkspaceGet or WindowCommandMode.WorkspaceSwitch
            or WindowCommandMode.WorkspaceMoveActive or WindowCommandMode.WorkspaceMoveWindow => [],
        _ => [],
    };

    public static string Normalize(string token)
    {
        ArgumentNullException.ThrowIfNull(token);
        var trimmed = token.Trim();
        return Focus.FirstOrDefault(candidate => string.Equals(candidate, trimmed, StringComparison.OrdinalIgnoreCase)) ?? trimmed;
    }

    public static bool IsAllowed(WindowCommandMode mode, string? token) =>
        token is not null && GetAllowedTokens(mode).Contains(token, StringComparer.Ordinal);

    public static bool RequiresValue(WindowTargetKind kind) => kind is not WindowTargetKind.Active;
}
