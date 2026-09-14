namespace CrossMacro.Infrastructure.Services.Playback;

internal readonly record struct WindowControlSelector(WindowTargetKind Kind, string Value)
{
    internal static string? Parse(string[] parts, string command, bool allowClass, out WindowControlSelector selector)
    {
        selector = default;
        var mode = allowClass ? WindowCommandMode.Focus : WindowCommandMode.Close;
        var allowedTokens = WindowSelectorSyntax.GetAllowedTokens(mode);
        if (parts.Length < 3)
        {
            return $"Syntax: window {command} {string.Join('|', allowedTokens)} <value>";
        }

        var field = parts[2].ToUpperInvariant();
        var token = allowedTokens.FirstOrDefault(candidate => string.Equals(candidate, parts[2], StringComparison.OrdinalIgnoreCase));
        var kind = token is not null
            ? WindowSelectorSyntax.ParseCanonical(token)
            : WindowTargetKind.Unknown;
        if (kind is WindowTargetKind.Unknown)
        {
            return $"Unknown field '{parts[2]}'. Expected: {string.Join(", ", allowedTokens)}.";
        }
        if (kind is WindowTargetKind.Active)
        {
            selector = new(kind, string.Empty);
            return parts.Length is 3 ? null : $"Syntax: window {command} active";
        }

        var value = Unquote(string.Join(' ', parts[3..]));
        if (string.IsNullOrWhiteSpace(value))
        {
            return $"Missing value for 'window {command} {field}'.";
        }
        selector = new(kind, value);
        return null;
    }
}
