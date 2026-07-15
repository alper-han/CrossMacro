namespace CrossMacro.Cli;

public sealed record class DoctorCliOptions(bool Verbose = false, bool JsonOutput = false, string? LogLevel = null)
    : CliCommandOptions(JsonOutput, LogLevel);
