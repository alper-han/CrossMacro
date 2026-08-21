namespace CrossMacro.Core.Models;

/// <summary>
/// The independent root lists used to authorize MCP file-backed operations.
/// </summary>
public enum McpPathSetting
{
    MacroRead = 0,
    MacroWrite = 1,
    ImageRead = 2,
    ImageWrite = 3,
    FileRead = 4,
    FileWrite = 5,
}
