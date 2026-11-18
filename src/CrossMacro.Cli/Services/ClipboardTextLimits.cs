namespace CrossMacro.Cli.Services;

/// <summary>The one clipboard text size limit, enforced by <see cref="ClipboardCliService"/> for every caller.</summary>
public static class ClipboardTextLimits
{
    public const int MaximumCharacters = 65_536;
}
