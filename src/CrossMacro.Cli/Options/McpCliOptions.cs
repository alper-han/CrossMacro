namespace CrossMacro.Cli.Options;

/// <summary>
/// Starts the local stdio Model Context Protocol server.
/// </summary>
public sealed record McpCliOptions(string? LogLevel = null, bool Restricted = false)
    : CliCommandOptions(JsonOutput: false, LogLevel);
