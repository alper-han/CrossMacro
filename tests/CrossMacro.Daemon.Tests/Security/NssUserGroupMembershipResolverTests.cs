namespace CrossMacro.Daemon.Tests.Security;

public sealed class NssUserGroupMembershipResolverTests
{
    [LinuxFact]
    public void LibcLookup_ShouldResolveRootThroughNss()
    {
        var lookup = new LibcNssUserGroupLookup();

        Assert.True(lookup.TryGetUser(0, out var root));
        Assert.True(lookup.TryGetGroupIds(root.Name, root.PrimaryGroupId, out var groupIds));
        Assert.Contains(root.PrimaryGroupId, groupIds);
    }

    [Fact]
    public void IsUserInGroup_WhenDirectoryIdentityHasSupplementaryGroup_ShouldAllow()
    {
        var lookup = new FakeNssUserGroupLookup
        {
            Users = { [100000u] = new NssUserIdentity("michael", 1000) },
            Groups = { ["crossmacro"] = 4242u },
            GroupIds = { ["michael"] = [1000u, 4242u] },
        };
        var resolver = new NssUserGroupMembershipResolver(lookup);

        var result = resolver.IsUserInGroup(100000u, "crossmacro");

        Assert.True(result);
        Assert.Equal(("michael", 1000u), lookup.GroupIdRequest);
    }

    [Fact]
    public void IsUserInGroup_WhenPrimaryGroupMatches_ShouldAllowWithoutSupplementaryLookup()
    {
        var lookup = new FakeNssUserGroupLookup
        {
            Users = { [1000u] = new NssUserIdentity("local-user", 4242) },
            Groups = { ["crossmacro"] = 4242u },
        };
        var resolver = new NssUserGroupMembershipResolver(lookup);

        var result = resolver.IsUserInGroup(1000u, "crossmacro");

        Assert.True(result);
        Assert.Null(lookup.GroupIdRequest);
    }

    [Fact]
    public void IsUserInGroup_WhenNssCannotResolveIdentity_ShouldDeny()
    {
        var lookup = new FakeNssUserGroupLookup
        {
            Groups = { ["crossmacro"] = 4242u },
        };
        var resolver = new NssUserGroupMembershipResolver(lookup);

        var result = resolver.IsUserInGroup(100000u, "crossmacro");

        Assert.False(result);
    }

    [Fact]
    public void IsUserInGroup_WhenSupplementaryLookupFails_ShouldDeny()
    {
        var lookup = new FakeNssUserGroupLookup
        {
            Users = { [100000u] = new NssUserIdentity("michael", 1000) },
            Groups = { ["crossmacro"] = 4242u },
            GroupLookupSucceeds = false,
        };
        var resolver = new NssUserGroupMembershipResolver(lookup);

        var result = resolver.IsUserInGroup(100000u, "crossmacro");

        Assert.False(result);
    }

    [Fact]
    public void IsUserInGroup_WhenLookupThrows_ShouldDeny()
    {
        var resolver = new NssUserGroupMembershipResolver(new ThrowingNssUserGroupLookup());

        var result = resolver.IsUserInGroup(1000u, "crossmacro");

        Assert.False(result);
    }

    private sealed class FakeNssUserGroupLookup : INssUserGroupLookup
    {
        public Dictionary<uint, NssUserIdentity> Users { get; } = [];
        public Dictionary<string, uint> Groups { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, IReadOnlyList<uint>> GroupIds { get; } = new(StringComparer.Ordinal);
        public bool GroupLookupSucceeds { get; init; } = true;
        public (string UserName, uint PrimaryGroupId)? GroupIdRequest { get; private set; }

        public bool TryGetUser(uint uid, out NssUserIdentity user) => Users.TryGetValue(uid, out user);

        public bool TryGetGroupId(string groupName, out uint gid) => Groups.TryGetValue(groupName, out gid);

        public bool TryGetGroupIds(string userName, uint primaryGroupId, out IReadOnlyList<uint> groupIds)
        {
            GroupIdRequest = (userName, primaryGroupId);
            if (GroupLookupSucceeds && GroupIds.TryGetValue(userName, out var resolvedGroupIds))
            {
                groupIds = resolvedGroupIds;
                return true;
            }

            groupIds = [];
            return false;
        }
    }

    private sealed class ThrowingNssUserGroupLookup : INssUserGroupLookup
    {
        public bool TryGetUser(uint uid, out NssUserIdentity user) => throw new InvalidOperationException();

        public bool TryGetGroupId(string groupName, out uint gid) => throw new InvalidOperationException();

        public bool TryGetGroupIds(string userName, uint primaryGroupId, out IReadOnlyList<uint> groupIds) => throw new InvalidOperationException();
    }

}
