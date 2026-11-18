
namespace CrossMacro.Daemon.Services;

internal sealed class PeerCredentialsProvider : IPeerCredentialsProvider
{
    private readonly NssUserGroupMembershipResolver _groupMembershipResolver;

    public PeerCredentialsProvider()
        : this(new NssUserGroupMembershipResolver())
    {
    }

    internal PeerCredentialsProvider(NssUserGroupMembershipResolver groupMembershipResolver)
    {
        _groupMembershipResolver = groupMembershipResolver ?? throw new ArgumentNullException(nameof(groupMembershipResolver));
    }

    public (uint Uid, uint Gid, int Pid)? GetCredentials(Socket socket) => PeerCredentials.GetCredentials(socket);
    public string? GetProcessExecutable(int pid) => PeerCredentials.GetProcessExecutable(pid);
    public bool IsUserInGroup(uint uid, string groupName) => _groupMembershipResolver.IsUserInGroup(uid, groupName);
}
