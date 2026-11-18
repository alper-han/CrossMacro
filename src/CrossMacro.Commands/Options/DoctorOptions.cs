namespace CrossMacro.Commands.Options;

public sealed record DoctorOptions(bool Verbose = false, bool JsonOutput = false, string? LogLevel = null)
    : CommandOptions(JsonOutput, LogLevel);
