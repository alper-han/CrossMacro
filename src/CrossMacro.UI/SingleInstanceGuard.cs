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

        var guard = TryAcquireCore(name);
        if (guard != null)
        {
            return guard;
        }

        // Regular desktop users may not have permission to create a Global mutex on Windows.
        // Fall back to a session-local lock so single-instance behavior still works.
        if (OperatingSystem.IsWindows() &&
            name.StartsWith(GlobalPrefix, StringComparison.Ordinal))
        {
            var localName = LocalPrefix + name[GlobalPrefix.Length..];
            guard = TryAcquireCore(localName);

            if (guard != null)
            {
                return guard;
            }
        }

        return null;
    }

    private static SingleInstanceGuard? TryAcquireCore(string name)
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
                return null;
            }

            if (!hasHandle)
            {
                mutex.Dispose();
                return null;
            }

            return new SingleInstanceGuard(mutex, hasHandle: true);
        }
        catch (UnauthorizedAccessException)
        {
            mutex?.Dispose();
            return null;
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
