namespace CrossMacro.Platform.Abstractions;

/// <summary>
/// Optional capability for platforms where the standard paste shortcut uses the Meta key
/// instead of Control, such as Command+V on macOS.
/// </summary>
public interface IPlatformPasteShortcutProvider
{
    bool UsesMetaKeyForStandardPaste { get; }
}
