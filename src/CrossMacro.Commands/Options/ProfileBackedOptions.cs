namespace CrossMacro.Commands.Options;

/// <summary>
/// Base for commands that read or run profile-owned state, so the profile store must be initialized before they execute.
/// </summary>
public abstract record ProfileBackedOptions(bool JsonOutput, string? LogLevel = null)
    : CommandOptions(JsonOutput, LogLevel);
