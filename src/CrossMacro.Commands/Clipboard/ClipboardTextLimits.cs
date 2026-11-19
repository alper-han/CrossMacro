namespace CrossMacro.Commands.Clipboard;

/// <summary>The one clipboard text size limit, enforced by <see cref="ClipboardCommandService"/> for every caller.</summary>
public static class ClipboardTextLimits
{
    public const int MaximumCharacters = 65_536;
}
