
namespace CrossMacro.Daemon.Services;

internal interface IPeerCredentialsProvider
{
    public (uint Uid, uint Gid, int Pid)? GetCredentials(Socket socket);
    public string? GetProcessExecutable(int pid);
    public bool IsUserInGroup(uint uid, string groupName);
}
