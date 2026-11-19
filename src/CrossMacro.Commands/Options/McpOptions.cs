namespace CrossMacro.Commands.Options;

/// <summary>
/// Starts the local stdio Model Context Protocol server.
/// </summary>
public sealed record McpOptions(string? LogLevel = null, bool Restricted = false)
    : ProfileBackedOptions(JsonOutput: false, LogLevel);
