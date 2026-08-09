namespace CrossMacro.Daemon.Security;

internal sealed class NssUserGroupMembershipResolver : IUserGroupMembershipResolver
{
    private readonly INssUserGroupLookup _lookup;

    public NssUserGroupMembershipResolver()
        : this(new LibcNssUserGroupLookup())
    {
    }

    internal NssUserGroupMembershipResolver(INssUserGroupLookup lookup)
    {
        _lookup = lookup ?? throw new ArgumentNullException(nameof(lookup));
    }

    public bool IsUserInGroup(uint uid, string groupName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(groupName))
            {
                return false;
            }

            if (!_lookup.TryGetGroupId(groupName, out var targetGroupId))
            {
                Log.Debug("[Security] NSS could not resolve group {Group}", groupName);
                return false;
            }

            if (!_lookup.TryGetUser(uid, out var user))
            {
                Log.Debug("[Security] NSS could not resolve UID {Uid}", uid);
                return false;
            }

            if (user.PrimaryGroupId == targetGroupId)
            {
                return true;
            }

            if (!_lookup.TryGetGroupIds(user.Name, user.PrimaryGroupId, out var groupIds))
            {
                Log.Debug("[Security] NSS could not resolve supplementary groups for UID {Uid}", uid);
                return false;
            }

            return groupIds.Contains(targetGroupId);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Warning(ex, "[Security] NSS group lookup failed for UID {Uid}", uid);
            return false;
        }
    }
}
