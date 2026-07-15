
namespace CrossMacro.Daemon.Services;

public interface IPeerCredentialsProvider
{
    (uint Uid, uint Gid, int Pid)? GetCredentials(Socket socket);
    string? GetProcessExecutable(int pid);
    bool IsUserInGroup(uint uid, string groupName);
}
