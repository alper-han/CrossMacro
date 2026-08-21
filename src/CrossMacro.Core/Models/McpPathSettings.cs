namespace CrossMacro.Core.Models;

/// <summary>
/// Explicit roots authorized for MCP file-backed operations. Empty roots leave the
/// operation unrestricted; configured roots constrain access to those directories.
/// </summary>
[method: System.Text.Json.Serialization.JsonConstructor]
public sealed class McpPathSettings(
    IReadOnlyList<string>? macroReadRoots,
    IReadOnlyList<string>? macroWriteRoots,
    IReadOnlyList<string>? imageReadRoots,
    IReadOnlyList<string>? imageWriteRoots,
    IReadOnlyList<string>? fileReadRoots,
    IReadOnlyList<string>? fileWriteRoots)
{
    public McpPathSettings()
        : this([], [], [], [], [], [])
    {
    }

    public IReadOnlyList<string> MacroReadRoots { get; } = NormalizeRoots(macroReadRoots);

    public IReadOnlyList<string> MacroWriteRoots { get; } = NormalizeRoots(macroWriteRoots);

    public IReadOnlyList<string> ImageReadRoots { get; } = NormalizeRoots(imageReadRoots);

    public IReadOnlyList<string> ImageWriteRoots { get; } = NormalizeRoots(imageWriteRoots);

    public IReadOnlyList<string> FileReadRoots { get; } = NormalizeRoots(fileReadRoots);

    public IReadOnlyList<string> FileWriteRoots { get; } = NormalizeRoots(fileWriteRoots);

    public IReadOnlyList<string> GetRoots(McpPathSetting setting) => setting switch
    {
        McpPathSetting.MacroRead => MacroReadRoots,
        McpPathSetting.MacroWrite => MacroWriteRoots,
        McpPathSetting.ImageRead => ImageReadRoots,
        McpPathSetting.ImageWrite => ImageWriteRoots,
        McpPathSetting.FileRead => FileReadRoots,
        McpPathSetting.FileWrite => FileWriteRoots,
        _ => throw new ArgumentOutOfRangeException(nameof(setting), setting, "Unknown MCP path setting."),
    };

    public McpPathSettings WithRoots(McpPathSetting setting, IEnumerable<string>? roots)
    {
        var normalizedRoots = NormalizeRoots(roots);
        return setting switch
        {
            McpPathSetting.MacroRead => new McpPathSettings(normalizedRoots, MacroWriteRoots, ImageReadRoots, ImageWriteRoots, FileReadRoots, FileWriteRoots),
            McpPathSetting.MacroWrite => new McpPathSettings(MacroReadRoots, normalizedRoots, ImageReadRoots, ImageWriteRoots, FileReadRoots, FileWriteRoots),
            McpPathSetting.ImageRead => new McpPathSettings(MacroReadRoots, MacroWriteRoots, normalizedRoots, ImageWriteRoots, FileReadRoots, FileWriteRoots),
            McpPathSetting.ImageWrite => new McpPathSettings(MacroReadRoots, MacroWriteRoots, ImageReadRoots, normalizedRoots, FileReadRoots, FileWriteRoots),
            McpPathSetting.FileRead => new McpPathSettings(MacroReadRoots, MacroWriteRoots, ImageReadRoots, ImageWriteRoots, normalizedRoots, FileWriteRoots),
            McpPathSetting.FileWrite => new McpPathSettings(MacroReadRoots, MacroWriteRoots, ImageReadRoots, ImageWriteRoots, FileReadRoots, normalizedRoots),
            _ => throw new ArgumentOutOfRangeException(nameof(setting), setting, "Unknown MCP path setting."),
        };
    }

    private static IReadOnlyList<string> NormalizeRoots(IEnumerable<string>? roots) =>
        Array.AsReadOnly(
            (roots ?? [])
                .Where(static root => !string.IsNullOrWhiteSpace(root))
                .Select(static root => root.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToArray());
}
