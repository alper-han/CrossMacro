
namespace CrossMacro.UI;

internal sealed class SingleInstanceGuard : IDisposable
{
    private const string GlobalPrefix = @"Global\";
    private const string LocalPrefix = @"Local\";
    private static readonly Lock UnixLockGate = new();
    private static readonly HashSet<string> HeldUnixLockPaths = [];

    private readonly Mutex? _mutex;
    private readonly FileStream? _lockFile;
    private readonly string? _unixLockPath;
    private bool _hasHandle;
    private int _disposed;

    private SingleInstanceGuard(Mutex mutex, bool hasHandle)
    {
        _mutex = mutex;
        _hasHandle = hasHandle;
    }

    private SingleInstanceGuard(FileStream lockFile, string unixLockPath)
    {
        _lockFile = lockFile;
        _unixLockPath = unixLockPath;
        _hasHandle = true;
    }

    public static SingleInstanceGuard? TryAcquire(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (OperatingSystem.IsLinux())
        {
            return TryAcquireUnixFileLock(name);
        }

        var preferredName = GetPreferredMutexName(name);

        var (guard, unauthorized) = TryAcquireCore(preferredName);
        if (guard is not null)
        {
            return guard;
        }

        // Only fall back to a session-local lock when the Global mutex could not be
        // created/accessed due to insufficient permissions.  If the Global mutex exists
        // and is already held by another instance, we must NOT fall back — doing so would
        // acquire a different (Local) mutex and allow a second instance to start.
        if (unauthorized &&
            OperatingSystem.IsWindows() &&
            preferredName.StartsWith(GlobalPrefix, StringComparison.Ordinal))
        {
            var localName = LocalPrefix + preferredName[GlobalPrefix.Length..];
            (guard, _) = TryAcquireCore(localName);

            if (guard is not null)
            {
                return guard;
            }
        }

        return null;
    }

    [SupportedOSPlatform("linux")]
    private static SingleInstanceGuard? TryAcquireUnixFileLock(string name)
    {
        var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(homeDirectory))
        {
            return null;
        }

        var lockDirectory = Path.Combine(homeDirectory, ".cache", "crossmacro");
        var lockName = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(name)));
        var lockPath = Path.Combine(lockDirectory, $"{lockName}.lock");

        lock (UnixLockGate)
        {
            if (!HeldUnixLockPaths.Add(lockPath))
            {
                return null;
            }

            try
            {
                _ = Directory.CreateDirectory(lockDirectory);
                var lockFile = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
                try
                {
                    lockFile.Lock(0, 1);
                    return new SingleInstanceGuard(lockFile, lockPath);
                }
                catch (IOException)
                {
                    lockFile.Dispose();
                    _ = HeldUnixLockPaths.Remove(lockPath);
                    return null;
                }
            }
            catch (IOException)
            {
                _ = HeldUnixLockPaths.Remove(lockPath);
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                _ = HeldUnixLockPaths.Remove(lockPath);
                return null;
            }
        }
    }

    private static string GetPreferredMutexName(string name)
    {
        if (!OperatingSystem.IsWindows())
        {
            return name;
        }

        if (name.StartsWith(GlobalPrefix, StringComparison.Ordinal) ||
            name.StartsWith(LocalPrefix, StringComparison.Ordinal))
        {
            return name;
        }

        return GlobalPrefix + name;
    }

    private static (SingleInstanceGuard? Guard, bool Unauthorized) TryAcquireCore(string name)
    {
        Mutex? mutex = null;
        bool hasHandle;

        try
        {
            mutex = new Mutex(initiallyOwned: false, name);

            try
            {
                hasHandle = mutex.WaitOne(0, exitContext: false);
            }
            catch (AbandonedMutexException)
            {
                hasHandle = true;
            }
            catch (UnauthorizedAccessException)
            {
                mutex.Dispose();
                return (null, true);
            }

            if (!hasHandle)
            {
                mutex.Dispose();
                return (null, false);
            }

            return (new SingleInstanceGuard(mutex, hasHandle: true), false);
        }
        catch (UnauthorizedAccessException)
        {
            mutex?.Dispose();
            return (null, true);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            mutex?.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) is not 0)
        {
            return;
        }

        if (_hasHandle)
        {
            try
            {
                _mutex?.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // Ignore release failures during shutdown.
            }
            finally
            {
                _hasHandle = false;
            }
        }

        if (_lockFile is not null)
        {
            if (OperatingSystem.IsLinux())
            {
                UnlockUnixFile(_lockFile);
            }

            _lockFile.Dispose();

            if (_unixLockPath is not null)
            {
                lock (UnixLockGate)
                {
                    _ = HeldUnixLockPaths.Remove(_unixLockPath);
                }
            }
        }

        _mutex?.Dispose();
    }

    [SupportedOSPlatform("linux")]
    private static void UnlockUnixFile(FileStream lockFile)
    {
        try
        {
            lockFile.Unlock(0, 1);
        }
        catch (IOException)
        {
            // The operating system already released the lock.
        }
    }
}
