
namespace CrossMacro.Daemon.Security;

/// <summary>
/// Provides SO_PEERCRED functionality for Unix domain sockets.
/// Retrieves the UID, GID, and PID of the connected peer process.
/// These credentials are provided by the kernel and cannot be spoofed.
/// </summary>
internal static partial class PeerCredentials
{
    private const int SOL_SOCKET = 1;
    private const int SO_PEERCRED = 17;

    [LibraryImport("libc", SetLastError = true)]
    private static partial int getsockopt(int socket, int level, int optname, byte[] optval, ref int optlen);

    /// <summary>
    /// Gets the peer credentials (UID, GID, PID) for a connected Unix domain socket.
    /// </summary>
    /// <param name="socket">The connected socket</param>
    /// <returns>Tuple of (uid, gid, pid) or null if failed</returns>
    public static (uint Uid, uint Gid, int Pid)? GetCredentials(Socket socket)
    {
        if (socket is null)
        {
            return null;
        }

        try
        {
            var credBuffer = new byte[12]; // sizeof(struct ucred) = 12 bytes
            var len = credBuffer.Length;

            var handle = (int)socket.Handle;
            if (getsockopt(handle, SOL_SOCKET, SO_PEERCRED, credBuffer, ref len) is 0)
            {
                var pid = BitConverter.ToInt32(credBuffer, 0);
                var uid = BitConverter.ToUInt32(credBuffer, 4);
                var gid = BitConverter.ToUInt32(credBuffer, 8);

                Log.Debug("[PeerCredentials] Retrieved: UID={Uid}, GID={Gid}, PID={Pid}", uid, gid, pid);
                return (uid, gid, pid);
            }
            var errno = Marshal.GetLastWin32Error();
            Log.Warning("[PeerCredentials] getsockopt failed with errno: {Errno}", errno);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Warning(ex, "[PeerCredentials] Failed to get peer credentials");
        }

        return null;
    }

    /// <summary>
    /// Gets the executable path for a given PID.
    /// </summary>
    public static string? GetProcessExecutable(int pid)
    {
        try
        {
            var linkPath = "/proc/" + pid.ToString(CultureInfo.InvariantCulture) + "/exe";
            if (System.IO.File.Exists(linkPath))
            {
                // ReadLink to resolve the symlink
                var target = new byte[4096];
                var result = readlink(linkPath, target, target.Length);
                if (result > 0)
                {
                    return System.Text.Encoding.UTF8.GetString(target, 0, result);
                }
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Debug(ex, "[PeerCredentials] Failed to get executable for PID {Pid}", pid);
        }
        return null;
    }

    [LibraryImport("libc", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int readlink(string path, byte[] buf, int bufsize);
}
