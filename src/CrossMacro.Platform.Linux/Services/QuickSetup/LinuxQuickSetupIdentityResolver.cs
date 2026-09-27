namespace CrossMacro.Platform.Linux.Services.QuickSetup;

internal sealed partial class LinuxQuickSetupIdentityResolver(
    Func<uint?> getEffectiveUid,
    Func<string?> getUidMap,
    Func<CancellationToken, ValueTask<uint?>> getHostUid)
{
    [LibraryImport("libc", SetLastError = true)]
    private static partial uint geteuid();

    private readonly Func<uint?> _getEffectiveUid = getEffectiveUid ?? throw new ArgumentNullException(nameof(getEffectiveUid));
    private readonly Func<string?> _getUidMap = getUidMap ?? throw new ArgumentNullException(nameof(getUidMap));
    private readonly Func<CancellationToken, ValueTask<uint?>> _getHostUid = getHostUid ?? throw new ArgumentNullException(nameof(getHostUid));

    public LinuxQuickSetupIdentityResolver()
        : this(TryGetEffectiveUid, static () => File.ReadAllText("/proc/self/uid_map"), ReadSystemBusUidAsync) { }

    public async ValueTask<LinuxQuickSetupIdentity?> ResolveAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var uid = _getEffectiveUid();
            if (uid is null or uint.MaxValue)
            {
                return null;
            }

            var fields = _getUidMap()?.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (fields is not { Length: 3 }
                || !uint.TryParse(fields[0], NumberStyles.None, CultureInfo.InvariantCulture, out var inside)
                || !uint.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out var outside)
                || !uint.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture, out var count)
                || inside != outside || count is 0 || uid < inside || uid.Value - inside >= count)
            {
                return null;
            }

            // A one-level uid_map cannot prove identity across nested namespaces.
            // Never translate it. Non-native, UID-preserving wrappers additionally
            // require the host system bus to attest the connecting process's UID.
            if ((inside is not 0 || count is not uint.MaxValue)
                && await _getHostUid(cancellationToken).ConfigureAwait(false) != uid)
            {
                return null;
            }

            return FromUid(uid.Value);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Debug(ex, "[LinuxQuickSetupIdentityResolver] Could not verify host UID");
            return null;
        }
    }

    internal static LinuxQuickSetupIdentity? FromHostUid(string? output)
        => uint.TryParse(output?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var uid) && uid is not uint.MaxValue
            ? FromUid(uid)
            : null;

    private static LinuxQuickSetupIdentity FromUid(uint uid)
    {
        var text = uid.ToString(CultureInfo.InvariantCulture);
        return new LinuxQuickSetupIdentity(text, $"uid:{text}");
    }

    private static async ValueTask<uint?> ReadSystemBusUidAsync(CancellationToken cancellationToken)
    {
        // Use the host system socket, not an environment-selected private/session bus.
        using var connection = new DBusConnection("unix:path=/run/dbus/system_bus_socket");
        var timeout = TimeSpan.FromSeconds(2);
        try
        {
            await LinuxDbusTransportBoundary.AwaitReplyAsync(connection.ConnectAsync().AsTask(), timeout, cancellationToken).ConfigureAwait(false);
            return await LinuxDbusTransportBoundary.AwaitReplyAsync(new SystemBusIdentityClient(connection).GetUidAsync(), timeout, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Debug(ex, "[LinuxQuickSetupIdentityResolver] Host system bus could not attest UID");
            return null;
        }
    }

    private sealed class SystemBusIdentityClient(DBusConnection connection)
        : LinuxDbusClientBase(connection, "org.freedesktop.DBus", "/org/freedesktop/DBus", "org.freedesktop.DBus")
    {
        public Task<uint> GetUidAsync() => CallAsync(
            "GetConnectionUnixUser",
            static (message, _) => message.GetBodyReader().ReadUInt32(),
            "s",
            (ref MessageWriter writer) => writer.WriteString(LinuxDbusTransportBoundary.GetUniqueDestination(Connection)));
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
