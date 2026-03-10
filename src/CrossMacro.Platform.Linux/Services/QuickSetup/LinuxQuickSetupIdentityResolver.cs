using System;
using System.Globalization;
using System.Runtime.InteropServices;
using Serilog;

namespace CrossMacro.Platform.Linux.Services.QuickSetup;

internal sealed class LinuxQuickSetupIdentityResolver
{
    [DllImport("libc", SetLastError = true)]
    private static extern uint geteuid();

    private readonly Func<string> _getUserName;
    private readonly Func<uint?> _getEffectiveUid;

    public LinuxQuickSetupIdentityResolver()
        : this(() => Environment.UserName, TryGetEffectiveUid)
    {
    }

    public LinuxQuickSetupIdentityResolver(Func<string> getUserName, Func<uint?> getEffectiveUid)
    {
        _getUserName = getUserName ?? throw new ArgumentNullException(nameof(getUserName));
        _getEffectiveUid = getEffectiveUid ?? throw new ArgumentNullException(nameof(getEffectiveUid));
    }

    public LinuxQuickSetupIdentity? Resolve()
    {
        var uid = _getEffectiveUid();
        if (uid.HasValue)
        {
            var uidText = uid.Value.ToString(CultureInfo.InvariantCulture);
            return new LinuxQuickSetupIdentity(uidText, $"uid:{uidText}");
        }

        var userName = _getUserName();
        if (string.IsNullOrWhiteSpace(userName))
        {
            return null;
        }

        var normalizedUserName = userName.Trim();
        if (HasControlCharacters(normalizedUserName))
        {
            return null;
        }

        return new LinuxQuickSetupIdentity(normalizedUserName, normalizedUserName);
    }

    private static bool HasControlCharacters(string value)
    {
        foreach (var c in value)
        {
            if (char.IsControl(c))
            {
                return true;
            }
        }

        return false;
    }

    private static uint? TryGetEffectiveUid()
    {
        if (!OperatingSystem.IsLinux())
        {
            return null;
        }

        try
        {
            return geteuid();
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "[LinuxQuickSetupIdentityResolver] Failed to read effective UID");
            return null;
        }
    }
}
