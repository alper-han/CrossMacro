namespace CrossMacro.Daemon.Security;

internal interface INssUserGroupLookup
{
    public bool TryGetUser(uint uid, out NssUserIdentity user);
    public bool TryGetGroupId(string groupName, out uint gid);
    public bool TryGetGroupIds(string userName, uint primaryGroupId, out IReadOnlyList<uint> groupIds);
}
