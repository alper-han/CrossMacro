using System;
using System.Threading;

namespace CrossMacro.UI;

internal sealed class SingleInstanceGuard : IDisposable
{
    private const string GlobalPrefix = @"Global\";
    private const string LocalPrefix = @"Local\";

    private readonly Mutex _mutex;
    private bool _hasHandle;

    private SingleInstanceGuard(Mutex mutex, bool hasHandle)
    {
        _mutex = mutex;
        _hasHandle = hasHandle;
    }

    public static SingleInstanceGuard? TryAcquire(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var (guard, unauthorized) = TryAcquireCore(name);
        if (guard != null)
        {
            return guard;
        }

        // Only fall back to a session-local lock when the Global mutex could not be
        // created/accessed due to insufficient permissions.  If the Global mutex exists
        // and is already held by another instance, we must NOT fall back — doing so would
        // acquire a different (Local) mutex and allow a second instance to start.
        if (unauthorized &&
            OperatingSystem.IsWindows() &&
            name.StartsWith(GlobalPrefix, StringComparison.Ordinal))
        {
            var localName = LocalPrefix + name[GlobalPrefix.Length..];
            (guard, _) = TryAcquireCore(localName);

            if (guard != null)
            {
                return guard;
            }
        }

        return null;
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
                hasHandle = mutex.WaitOne(0, false);
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
        catch
        {
            mutex?.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        if (_hasHandle)
        {
            try
            {
                _mutex.ReleaseMutex();
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

        _mutex.Dispose();
    }
}
