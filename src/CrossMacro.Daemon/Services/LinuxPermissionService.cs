using System;
using System.IO;
using System.Runtime.InteropServices;
using CrossMacro.Core.Logging;
using CrossMacro.Infrastructure.Linux.Native;

namespace CrossMacro.Daemon.Services;

public class LinuxPermissionService : ILinuxPermissionService
{
    private const int UnchangedOwner = -1;

    private readonly Func<string, bool> _fileExists;
    private readonly Func<string, IEnumerable<string>> _readLines;
    private readonly Func<string, int, int, int> _chown;
    private readonly Action<string> _setSocketPermissions;

    [DllImport("libc", EntryPoint = "chown", SetLastError = true)]
    private static extern int NativeChown(string path, int owner, int group);

    public LinuxPermissionService()
        : this(
            File.Exists,
            File.ReadLines,
            NativeChown,
            SetUnixSocketPermissions)
    {
    }

    internal LinuxPermissionService(
        Func<string, bool> fileExists,
        Func<string, IEnumerable<string>> readLines,
        Func<string, int, int, int> chown,
        Action<string> setSocketPermissions)
    {
        _fileExists = fileExists ?? throw new ArgumentNullException(nameof(fileExists));
        _readLines = readLines ?? throw new ArgumentNullException(nameof(readLines));
        _chown = chown ?? throw new ArgumentNullException(nameof(chown));
        _setSocketPermissions = setSocketPermissions ?? throw new ArgumentNullException(nameof(setSocketPermissions));
    }

    public void ConfigureSocketPermissions(string socketPath)
    {
        var targetGid = ResolveCrossMacroGroupGid();

        if (targetGid != -1)
        {
            SetSocketGroup(socketPath, targetGid);
        }

        // Restricted: RW for User and Group (660). Keep this independent from chown so
        // native ownership failures do not leave the socket at the runtime default mode.
        try
        {
            _setSocketPermissions(socketPath);
        }
        catch (FileNotFoundException ex)
        {
            Log.Warning("Socket path disappeared before file mode could be set: {Msg}", ex.Message);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to set socket file mode");
            throw;
        }
    }

    private int ResolveCrossMacroGroupGid()
    {
        try
        {
            if (!_fileExists(LinuxSystemPaths.GroupFile))
            {
                return -1;
            }

            foreach (var line in _readLines(LinuxSystemPaths.GroupFile))
            {
                if (!line.StartsWith("crossmacro:", StringComparison.Ordinal))
                {
                    continue;
                }

                var parts = line.Split(':');
                if (parts.Length >= 3 && int.TryParse(parts[2], out var gid))
                {
                    return gid;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning("Failed to lookup crossmacro group GID: {Msg}", ex.Message);
        }

        return -1;
    }

    private void SetSocketGroup(string socketPath, int targetGid)
    {
        try
        {
            // -1 means don't change owner.
            if (_chown(socketPath, UnchangedOwner, targetGid) == 0)
            {
                Log.Information("Set socket group to 'crossmacro' (GID: {Gid})", targetGid);
                return;
            }

            Log.Warning("Failed to chown socket to crossmacro group. Errno: {Err}", Marshal.GetLastWin32Error());
        }
        catch (Exception ex)
        {
            Log.Warning("Failed to chown socket to crossmacro group: {Msg}", ex.Message);
        }
    }

    private static void SetUnixSocketPermissions(string socketPath)
    {
#pragma warning disable CA1416 // CrossMacro.Daemon is the privileged Linux daemon runtime.
        File.SetUnixFileMode(socketPath, 
            UnixFileMode.UserRead | UnixFileMode.UserWrite | 
            UnixFileMode.GroupRead | UnixFileMode.GroupWrite);
#pragma warning restore CA1416
        Log.Information("Socket permissions set to 660 (User+Group RW)");
    }
}
