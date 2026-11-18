namespace CrossMacro.Daemon.Tests.Services;

public sealed class LinuxPermissionServiceTests
{
    private const string SocketPath = "/run/crossmacro/crossmacro.sock";

    [LinuxFact]
    public void ConfigureSocketPermissions_WhenSocketPathDoesNotExist_DoesNotThrow()
    {
        var service = new LinuxPermissionService();
        var missingPath = Path.Combine(Path.GetTempPath(), $"crossmacro-permission-test-missing-{Guid.NewGuid():N}");

        var ex = Record.Exception(() => service.ConfigureSocketPermissions(missingPath));

        Assert.Null(ex);
    }

    [LinuxFact]
    public void ConfigureSocketPermissions_WhenSocketPathExists_AppliesGroupReadWriteMode()
    {
        var service = new LinuxPermissionService();
        var socketPath = Path.Combine(Path.GetTempPath(), $"crossmacro-permission-test-existing-{Guid.NewGuid():N}");
        File.WriteAllText(socketPath, string.Empty);

        try
        {
            service.ConfigureSocketPermissions(socketPath);

            Assert.Equal(
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.GroupWrite,
                File.GetUnixFileMode(socketPath));
        }
        finally
        {
            File.Delete(socketPath);
        }
    }

    [Fact]
    public void ConfigureSocketPermissions_WhenCrossmacroGroupResolves_ChownsSocketToGroupAndSetsMode()
    {
        var chownCalls = new List<(string Path, uint Owner, uint Group)>();
        var modePaths = new List<string>();
        var service = new LinuxPermissionService(
            new FakeGroupLookup { GroupId = 4242 },
            chown: (path, owner, group) =>
            {
                chownCalls.Add((path, owner, group));
                return 0;
            },
            setSocketPermissions: path => modePaths.Add(path));

        service.ConfigureSocketPermissions(SocketPath);

        var call = Assert.Single(chownCalls);
        Assert.Equal((SocketPath, uint.MaxValue, 4242u), call);
        Assert.Equal([SocketPath], modePaths);
    }

    [Fact]
    public void ConfigureSocketPermissions_WhenGroupIdExceedsSignedRange_PassesItUnchangedToChown()
    {
        var chownCalls = new List<(string Path, uint Owner, uint Group)>();
        var service = new LinuxPermissionService(
            new FakeGroupLookup { GroupId = 3_000_000_000 },
            chown: (path, owner, group) =>
            {
                chownCalls.Add((path, owner, group));
                return 0;
            },
            setSocketPermissions: _ => { });

        service.ConfigureSocketPermissions(SocketPath);

        Assert.Equal(3_000_000_000u, Assert.Single(chownCalls).Group);
    }

    [Fact]
    public void ConfigureSocketPermissions_WhenCrossmacroGroupMissing_SkipsChownButStillSetsMode()
    {
        var chownCalls = new List<(string Path, uint Owner, uint Group)>();
        var modePaths = new List<string>();
        var service = new LinuxPermissionService(
            new FakeGroupLookup { GroupId = null },
            chown: (path, owner, group) =>
            {
                chownCalls.Add((path, owner, group));
                return 0;
            },
            setSocketPermissions: path => modePaths.Add(path));

        service.ConfigureSocketPermissions(SocketPath);

        Assert.Empty(chownCalls);
        Assert.Equal([SocketPath], modePaths);
    }

    [Fact]
    public void ConfigureSocketPermissions_WhenGroupLookupThrows_DoesNotChownButStillSetsMode()
    {
        var chownCalls = new List<(string Path, uint Owner, uint Group)>();
        var modePaths = new List<string>();
        var service = new LinuxPermissionService(
            new FakeGroupLookup { Exception = new IOException("nss unavailable") },
            chown: (path, owner, group) =>
            {
                chownCalls.Add((path, owner, group));
                return 0;
            },
            setSocketPermissions: path => modePaths.Add(path));

        service.ConfigureSocketPermissions(SocketPath);

        Assert.Empty(chownCalls);
        Assert.Equal([SocketPath], modePaths);
    }

    [Fact]
    public void ConfigureSocketPermissions_WhenChownThrows_StillSetsMode()
    {
        var modePaths = new List<string>();
        var service = new LinuxPermissionService(
            new FakeGroupLookup { GroupId = 4242 },
            chown: (_, _, _) => throw new EntryPointNotFoundException("chown unavailable"),
            setSocketPermissions: path => modePaths.Add(path));

        service.ConfigureSocketPermissions(SocketPath);

        Assert.Equal([SocketPath], modePaths);
    }

    [Fact]
    public void ConfigureSocketPermissions_WhenChownFailsAndModeFails_ThrowsModeFailure()
    {
        var chownCalls = new List<(string Path, uint Owner, uint Group)>();
        var service = new LinuxPermissionService(
            new FakeGroupLookup { GroupId = 4242 },
            chown: (path, owner, group) =>
            {
                chownCalls.Add((path, owner, group));
                return -1;
            },
            setSocketPermissions: _ => throw new IOException("chmod failed"));

        var ex = Assert.Throws<IOException>(() => service.ConfigureSocketPermissions(SocketPath));

        Assert.Equal("chmod failed", ex.Message);
        _ = Assert.Single(chownCalls);
    }

    [Fact]
    public void ConfigureSocketPermissions_WhenChownSucceedsAndModeFails_ThrowsModeFailure()
    {
        var service = new LinuxPermissionService(
            new FakeGroupLookup { GroupId = 4242 },
            chown: (_, _, _) => 0,
            setSocketPermissions: _ => throw new IOException("chmod failed"));

        var ex = Assert.Throws<IOException>(() => service.ConfigureSocketPermissions(SocketPath));

        Assert.Equal("chmod failed", ex.Message);
    }

    private sealed class FakeGroupLookup : INssUserGroupLookup
    {
        public uint? GroupId { get; init; }

        public Exception? Exception { get; init; }

        public bool TryGetUser(uint uid, out NssUserIdentity user) => throw new NotSupportedException();

        public bool TryGetGroupId(string groupName, out uint gid)
        {
            if (Exception is not null)
            {
                throw Exception;
            }

            Assert.Equal(DaemonAccessGroup.Name, groupName);
            gid = GroupId.GetValueOrDefault();
            return GroupId is not null;
        }

        public bool TryGetGroupIds(string userName, uint primaryGroupId, out IReadOnlyList<uint> groupIds) =>
            throw new NotSupportedException();
    }
}
