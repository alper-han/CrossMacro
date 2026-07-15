namespace CrossMacro.Platform.Abstractions.Diagnostics;

public readonly record struct LinuxDaemonSocketProbeOptions(
    string SocketPath,
    string RequiredGroupName);
