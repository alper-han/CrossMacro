namespace CrossMacro.Platform.Linux.Services.Runtime;

/// <summary>
/// <c>statx(2)</c> binding. Unlike <c>struct stat</c>, <c>struct statx</c> has one fixed layout on every
/// architecture, so the same definition is correct on x86_64 and arm64.
/// </summary>
internal static partial class LinuxStatx
{
    internal const int StructSize = 256;
    internal const int AtFdCwd = -100;
    internal const int AtSymlinkNoFollow = 0x100;
    internal const uint MaskType = 0x1;
    internal const uint MaskMode = 0x2;
    internal const uint MaskUid = 0x8;
    internal const uint MaskGid = 0x10;

    [StructLayout(LayoutKind.Explicit, Size = StructSize)]
    internal struct Statx
    {
        [FieldOffset(0)]
        public uint Mask;

        [FieldOffset(20)]
        public uint UserId;

        [FieldOffset(24)]
        public uint GroupId;

        [FieldOffset(28)]
        public ushort Mode;
    }

    internal static int NoFollow(string path, out Statx result) =>
        statx(AtFdCwd, path, AtSymlinkNoFollow, MaskType | MaskMode | MaskUid | MaskGid, out result);

    [LibraryImport("libc", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int statx(int dirFd, string path, int flags, uint mask, out Statx buffer);
}
