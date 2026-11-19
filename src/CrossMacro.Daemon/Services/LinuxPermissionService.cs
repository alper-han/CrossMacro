using System.Runtime.InteropServices;
using CrossMacro.Daemon.Security;

namespace CrossMacro.Daemon.Services;

internal sealed partial class LinuxPermissionService : ILinuxPermissionService
{
    // chown treats (uid_t)-1 as "leave unchanged".
    private const uint UnchangedOwner = uint.MaxValue;

    private readonly INssUserGroupLookup _groupLookup;
    private readonly Func<string, uint, uint, int> _chown;
    private readonly Action<string> _setSocketPermissions;

    [LibraryImport("libc", EntryPoint = "chown", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int NativeChown(string path, uint owner, uint group);

    public LinuxPermissionService()
        : this(
            new LibcNssUserGroupLookup(),
            NativeChown,
            SetUnixSocketPermissions)
    { /* Empty */ }

    internal LinuxPermissionService(
        INssUserGroupLookup groupLookup,
        Func<string, uint, uint, int> chown,
        Action<string> setSocketPermissions)
    {
        _groupLookup = groupLookup ?? throw new ArgumentNullException(nameof(groupLookup));
        _chown = chown ?? throw new ArgumentNullException(nameof(chown));
        _setSocketPermissions = setSocketPermissions ?? throw new ArgumentNullException(nameof(setSocketPermissions));
    }

    public void ConfigureSocketPermissions(string socketPath)
    {
        if (TryResolveCrossMacroGroupId(out var targetGid))
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
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "Failed to set socket file mode");
            throw;
        }
    }

    private bool TryResolveCrossMacroGroupId(out uint gid)
    {
        try
        {
            if (_groupLookup.TryGetGroupId(DaemonAccessGroup.Name, out gid))
            {
                return true;
            }

            Log.Warning("NSS could not resolve the '{Group}' group; the socket keeps its default group", DaemonAccessGroup.Name);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Warning("Failed to lookup crossmacro group GID: {Msg}", ex.Message);
        }

        gid = default;
        return false;
    }

    private void SetSocketGroup(string socketPath, uint targetGid)
    {
        try
        {
            if (_chown(socketPath, UnchangedOwner, targetGid) is 0)
            {
                Log.Information("Set socket group to 'crossmacro' (GID: {Gid})", targetGid);
                return;
            }

            Log.Warning("Failed to chown socket to crossmacro group. Errno: {Err}", Marshal.GetLastWin32Error());
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Warning("Failed to chown socket to crossmacro group: {Msg}", ex.Message);
        }
    }

    private static void SetUnixSocketPermissions(string socketPath)
    {
        File.SetUnixFileMode(socketPath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite |
            UnixFileMode.GroupRead | UnixFileMode.GroupWrite);
        Log.Information("Socket permissions set to 660 (User+Group RW)");
    }
}
