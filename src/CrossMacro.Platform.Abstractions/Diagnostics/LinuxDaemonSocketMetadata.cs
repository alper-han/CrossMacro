using System.IO;

namespace CrossMacro.Platform.Abstractions.Diagnostics;

public readonly record struct LinuxDaemonSocketMetadata(
    string Path,
    LinuxFileSystemEntryKind EntryKind,
    int? OwnerUserId = null,
    int? OwnerGroupId = null,
    UnixFileMode? Permissions = null);
