namespace CrossMacro.Cli.Options;

/// <summary>
/// Base for commands that read or run profile-owned state, so the profile store must be initialized before they execute.
/// </summary>
public abstract record ProfileBackedCliOptions(bool JsonOutput, string? LogLevel = null)
    : CliCommandOptions(JsonOutput, LogLevel);
