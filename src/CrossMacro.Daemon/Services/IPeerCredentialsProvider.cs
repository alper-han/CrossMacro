
namespace CrossMacro.Daemon.Services;

internal interface IPeerCredentialsProvider
{
    public (uint Uid, uint Gid, int Pid)? GetCredentials(Socket socket);
    public bool IsUserInGroup(uint uid, string groupName);
}
