namespace CrossMacro.Daemon.Security;

internal sealed partial class LibcNssUserGroupLookup : INssUserGroupLookup
{
    private const int Erange = 34;
    private const int InitialBufferSize = 16 * 1024;
    private const int MaximumBufferSize = 1024 * 1024;
    private const int InitialGroupCapacity = 16;
    private const int MaximumGroupCapacity = 65536;

    public bool TryGetUser(uint uid, out NssUserIdentity user)
    {
        foreach (var buffer in EnumerateBuffers())
        {
            var password = default(NativePasswd);
            var error = getpwuid_r(uid, out password, buffer, (nuint)buffer.Length, out var result);
            if (error is 0 && result != IntPtr.Zero)
            {
                var userName = Marshal.PtrToStringUTF8(password.Name);
                if (!string.IsNullOrWhiteSpace(userName))
                {
                    user = new NssUserIdentity(userName, password.GroupId);
                    return true;
                }

                break;
            }

            if (error is not Erange)
            {
                break;
            }
        }

        user = default;
        return false;
    }

    public bool TryGetGroupId(string groupName, out uint gid)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupName);

        foreach (var buffer in EnumerateBuffers())
        {
            var group = default(NativeGroup);
            var error = getgrnam_r(groupName, out group, buffer, (nuint)buffer.Length, out var result);
            if (error is 0 && result != IntPtr.Zero)
            {
                gid = group.GroupId;
                return true;
            }

            if (error is not Erange)
            {
                break;
            }
        }

        gid = default;
        return false;
    }

    public bool TryGetGroupIds(string userName, uint primaryGroupId, out IReadOnlyList<uint> groupIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);

        var capacity = InitialGroupCapacity;
        while (capacity <= MaximumGroupCapacity)
        {
            var groups = new uint[capacity];
            var count = groups.Length;
            if (getgrouplist(userName, primaryGroupId, groups, ref count) >= 0)
            {
                groupIds = groups[..Math.Min(count, groups.Length)];
                return true;
            }

            if (count <= capacity || count > MaximumGroupCapacity)
            {
                break;
            }

            capacity = count;
        }

        groupIds = [];
        return false;
    }

    private static IEnumerable<byte[]> EnumerateBuffers()
    {
        for (var size = InitialBufferSize; size <= MaximumBufferSize; size *= 2)
        {
            yield return new byte[size];
        }
    }

    [LibraryImport("libc")]
    private static partial int getpwuid_r(
        uint uid,
        out NativePasswd password,
        byte[] buffer,
        nuint bufferLength,
        out IntPtr result);

    [LibraryImport("libc", StringMarshalling = StringMarshalling.Utf8)]
    private static partial int getgrnam_r(
        string groupName,
        out NativeGroup group,
        byte[] buffer,
        nuint bufferLength,
        out IntPtr result);

    [LibraryImport("libc", StringMarshalling = StringMarshalling.Utf8)]
    private static partial int getgrouplist(
        string userName,
        uint primaryGroupId,
        [Out] uint[] groupIds,
        ref int groupCount);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePasswd
    {
        public IntPtr Name;
        public IntPtr Password;
        public uint UserId;
        public uint GroupId;
        public IntPtr Gecos;
        public IntPtr HomeDirectory;
        public IntPtr Shell;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeGroup
    {
        public IntPtr Name;
        public IntPtr Password;
        public uint GroupId;
        public IntPtr Members;
    }
}
