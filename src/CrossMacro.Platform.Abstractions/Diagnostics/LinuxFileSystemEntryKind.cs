namespace CrossMacro.Platform.Abstractions.Diagnostics;

public enum LinuxFileSystemEntryKind
{
    Unknown = 0,
    Missing = 1,
    Socket = 2,
    Directory = 3,
    File = 4,
    Other = 5,
}
