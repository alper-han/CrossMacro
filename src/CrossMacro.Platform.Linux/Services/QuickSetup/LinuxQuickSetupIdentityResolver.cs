
namespace CrossMacro.Platform.Linux.Services.QuickSetup;

internal sealed partial class LinuxQuickSetupIdentityResolver(Func<string> getUserName, Func<uint?> getEffectiveUid)
{
    [LibraryImport("libc", SetLastError = true)]
    private static partial uint geteuid();

    private readonly Func<string> _getUserName = getUserName ?? throw new ArgumentNullException(nameof(getUserName));
    private readonly Func<uint?> _getEffectiveUid = getEffectiveUid ?? throw new ArgumentNullException(nameof(getEffectiveUid));

    public LinuxQuickSetupIdentityResolver()
        : this(static () => Environment.UserName, TryGetEffectiveUid) { /* Empty */ }

    public LinuxQuickSetupIdentity? Resolve()
    {
        var uid = _getEffectiveUid();
        if (uid is not null)
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
        return value.Any(char.IsControl);
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
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Debug(ex, "[LinuxQuickSetupIdentityResolver] Failed to read effective UID");
            return null;
        }
    }
}
