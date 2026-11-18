namespace CrossMacro.Platform.Linux.Tests.Services;

public sealed class LinuxStatxTests
{
    // Offsets and total size from include/uapi/linux/stat.h; struct statx is identical on every architecture.
    [Fact]
    public void Layout_MatchesKernelStatxAbi()
    {
        Assert.Equal(256, Marshal.SizeOf<LinuxStatx.Statx>());
        Assert.Equal(0, (int)Marshal.OffsetOf<LinuxStatx.Statx>(nameof(LinuxStatx.Statx.Mask)));
        Assert.Equal(20, (int)Marshal.OffsetOf<LinuxStatx.Statx>(nameof(LinuxStatx.Statx.UserId)));
        Assert.Equal(24, (int)Marshal.OffsetOf<LinuxStatx.Statx>(nameof(LinuxStatx.Statx.GroupId)));
        Assert.Equal(28, (int)Marshal.OffsetOf<LinuxStatx.Statx>(nameof(LinuxStatx.Statx.Mode)));
    }

    [LinuxFact]
    public void NoFollow_RegularFile_ReportsRegularTypeAndPermissions()
    {
        var path = Path.Combine(Path.GetTempPath(), $"crossmacro-statx-{Guid.NewGuid():N}");
        File.WriteAllText(path, "x");
        try
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead);

            Assert.Equal(0, LinuxStatx.NoFollow(path, out var stat));

            Assert.Equal(0x8000, stat.Mode & 0xF000);
            Assert.Equal(
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead,
                (UnixFileMode)(stat.Mode & 0x0FFF));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [LinuxFact]
    public void NoFollow_SocketAndDirectory_ReportTheirFileType()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cm-statx-{Guid.NewGuid():N}.sock");
        using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        socket.Bind(new UnixDomainSocketEndPoint(path));
        try
        {
            Assert.Equal(0, LinuxStatx.NoFollow(path, out var stat));
            Assert.Equal(0xC000, stat.Mode & 0xF000);

            Assert.Equal(0, LinuxStatx.NoFollow(Path.GetTempPath(), out var directory));
            Assert.Equal(0x4000, directory.Mode & 0xF000);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [LinuxFact]
    public void NoFollow_MissingPath_Fails()
    {
        var path = Path.Combine(Path.GetTempPath(), $"crossmacro-missing-{Guid.NewGuid():N}");

        Assert.NotEqual(0, LinuxStatx.NoFollow(path, out _));
    }
}
