using System.Net.Sockets;
using CrossMacro.Daemon.Security;

namespace CrossMacro.Daemon.Services;

public sealed class PeerCredentialsProvider : IPeerCredentialsProvider
{
    public (uint Uid, uint Gid, int Pid)? GetCredentials(Socket socket) => PeerCredentials.GetCredentials(socket);
    public string? GetProcessExecutable(int pid) => PeerCredentials.GetProcessExecutable(pid);
    public bool IsUserInGroup(uint uid, string groupName) => PeerCredentials.IsUserInGroup(uid, groupName);
}
