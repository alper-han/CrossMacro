namespace CrossMacro.UI.Models;

/// <summary>The on-disk naming conventions of macro files.</summary>
internal static class MacroFileFormat
{
    internal const string Extension = ".macro";

    internal static bool HasMacroExtension(string path) => path.EndsWith(Extension, StringComparison.OrdinalIgnoreCase);

    internal static string WithoutExtension(string name) =>
        HasMacroExtension(name) ? name[..^Extension.Length] : name;

    internal static FileDialogFilter[] CreateDialogFilters(string displayName) =>
        [new FileDialogFilter { Name = displayName, Extensions = [Extension[1..]] }];
}
