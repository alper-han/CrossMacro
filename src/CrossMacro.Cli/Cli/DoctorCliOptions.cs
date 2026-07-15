namespace CrossMacro.Cli;

public sealed record DoctorCliOptions(bool Verbose = false, bool JsonOutput = false, string? LogLevel = null)
    : CliCommandOptions(JsonOutput, LogLevel);
