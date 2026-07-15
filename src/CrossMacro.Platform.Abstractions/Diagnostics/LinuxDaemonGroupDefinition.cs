
namespace CrossMacro.Platform.Abstractions.Diagnostics;

public sealed record class LinuxDaemonGroupDefinition(
    string Name,
    int GroupId,
    IReadOnlyCollection<string> MemberNames);
