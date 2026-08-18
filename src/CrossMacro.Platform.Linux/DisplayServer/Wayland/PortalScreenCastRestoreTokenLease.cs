using CrossMacro.Core;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal static class PortalScreenCastRestoreTokenLease
{
    private static readonly SemaphoreSlim ProcessGate = new(1, 1);
    private static readonly TimeSpan AcquireTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(25);
    private const string LockFileName = ".portal-screen-cast-token.lock";

    public static Task<IDisposable> AcquireAsync(CancellationToken cancellationToken) =>
        AcquireAsync(CrossMacro.Core.PathHelper.GetConfigDirectory(), cancellationToken);

    internal static async Task<IDisposable> AcquireAsync(string configDirectory, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configDirectory);
        await ProcessGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var lockPath = Path.Combine(configDirectory, LockFileName);
            Directory.CreateDirectory(Path.GetDirectoryName(lockPath)!);
            var stopwatch = Stopwatch.StartNew();

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var stream = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite, 1, FileOptions.Asynchronous);
                try
                {
                    stream.Lock(0, 1);
                    return new Lease(stream);
                }
                catch (IOException) when (stopwatch.Elapsed < AcquireTimeout)
                {
                    await stream.DisposeAsync().ConfigureAwait(false);
                    await Task.Delay(RetryDelay, TimeProvider.System, cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    await stream.DisposeAsync().ConfigureAwait(false);
                    throw;
                }
            }
        }
        catch
        {
            _ = ProcessGate.Release();
            throw;
        }
    }

    private sealed class Lease(FileStream initialStream) : IDisposable
    {
        private FileStream? _stream = initialStream ?? throw new ArgumentNullException(nameof(initialStream));

        public void Dispose()
        {
            var stream = Interlocked.Exchange(ref _stream, value: null);
            if (stream is null)
            {
                return;
            }

            try
            {
                stream.Unlock(0, 1);
            }
            catch (IOException ex)
            {
                Log.Debug(ex, "Portal restore-token lock was already released.");
            }
            finally
            {
                stream.DisposeAsync().AsTask().GetAwaiter().GetResult();
                _ = ProcessGate.Release();
            }
        }
    }
}
