using System;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Serilog;

namespace CrossMacro.Daemon.Security;

/// <summary>
/// Provides SO_PEERCRED functionality for Unix domain sockets.
/// Retrieves the UID, GID, and PID of the connected peer process.
/// These credentials are provided by the kernel and cannot be spoofed.
/// </summary>
public static class PeerCredentials
{
    private const int SOL_SOCKET = 1;
    private const int SO_PEERCRED = 17;

    [DllImport("libc", SetLastError = true)]
    private static extern int getsockopt(int socket, int level, int optname, byte[] optval, ref int optlen);

    /// <summary>
    /// Gets the peer credentials (UID, GID, PID) for a connected Unix domain socket.
    /// </summary>
    /// <param name="socket">The connected socket</param>
    /// <returns>Tuple of (uid, gid, pid) or null if failed</returns>
    public static (uint Uid, uint Gid, int Pid)? GetCredentials(Socket socket)
    {
        if (socket == null)
            return null;

        try
        {
            var credBuffer = new byte[12]; // sizeof(struct ucred) = 12 bytes
            var len = credBuffer.Length;

            var handle = (int)socket.Handle;
            if (getsockopt(handle, SOL_SOCKET, SO_PEERCRED, credBuffer, ref len) == 0)
            {
                var pid = BitConverter.ToInt32(credBuffer, 0);
                var uid = BitConverter.ToUInt32(credBuffer, 4);
                var gid = BitConverter.ToUInt32(credBuffer, 8);
                
                Log.Debug("[PeerCredentials] Retrieved: UID={Uid}, GID={Gid}, PID={Pid}", uid, gid, pid);
                return (uid, gid, pid);
            }
            else
            {
                var errno = Marshal.GetLastWin32Error();
                Log.Warning("[PeerCredentials] getsockopt failed with errno: {Errno}", errno);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[PeerCredentials] Failed to get peer credentials");
        }

        return null;
    }

    /// <summary>
    /// Checks if a user (by UID) is a member of a specific group.
    /// </summary>
    public static bool IsUserInGroup(uint uid, string groupName)
    {
        try
        {
            // 1. Get the group's GID
            int? groupGid = null;
            if (System.IO.File.Exists("/etc/group"))
            {
                foreach (var line in System.IO.File.ReadLines("/etc/group"))
                {
                    var parts = line.Split(':');
                    if (parts.Length >= 4 && parts[0] == groupName)
                    {
                        if (int.TryParse(parts[2], out int gid))
                        {
                            groupGid = gid;
                            
                            // Also check if UID is in the member list (field 4)
                            var members = parts[3].Split(',', StringSplitOptions.RemoveEmptyEntries);
                            var username = GetUsernameByUid(uid);
                            if (!string.IsNullOrEmpty(username) && Array.Exists(members, m => m == username))
                            {
                                return true;
                            }
                        }
                        break;
                    }
                }
            }

            if (groupGid == null)
            {
                Log.Debug("[PeerCredentials] Group '{Group}' not found", groupName);
                return false;
            }

            // 2. Check if user's primary group matches
            if (System.IO.File.Exists("/etc/passwd"))
            {
                foreach (var line in System.IO.File.ReadLines("/etc/passwd"))
                {
                    var parts = line.Split(':');
                    if (parts.Length >= 4 && int.TryParse(parts[2], out int userUid) && userUid == uid)
                    {
                        if (int.TryParse(parts[3], out int userGid) && userGid == groupGid)
                        {
                            return true;
                        }
                        break;
                    }
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[PeerCredentials] Failed to check group membership");
            return false;
        }
    }

    /// <summary>
    /// Gets the username for a given UID.
    /// </summary>
    public static string? GetUsernameByUid(uint uid)
    {
        try
        {
            if (System.IO.File.Exists("/etc/passwd"))
            {
                foreach (var line in System.IO.File.ReadLines("/etc/passwd"))
                {
                    var parts = line.Split(':');
                    if (parts.Length >= 3 && int.TryParse(parts[2], out int userUid) && userUid == uid)
                    {
                        return parts[0];
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "[PeerCredentials] Failed to get username for UID {Uid}", uid);
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
            var linkPath = $"/proc/{pid}/exe";
            if (System.IO.File.Exists(linkPath))
            {
                // ReadLink to resolve the symlink
                var target = new System.Text.StringBuilder(4096);
                var result = readlink(linkPath, target, target.Capacity);
                if (result > 0)
                {
                    return target.ToString(0, result);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "[PeerCredentials] Failed to get executable for PID {Pid}", pid);
        }
        return null;
    }

    [DllImport("libc", SetLastError = true)]
    private static extern int readlink(string path, System.Text.StringBuilder buf, int bufsize);
}
