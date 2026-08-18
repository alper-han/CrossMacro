namespace CrossMacro.Daemon.Security;

internal interface IUserGroupMembershipResolver
{
    public bool IsUserInGroup(uint uid, string groupName);
}
