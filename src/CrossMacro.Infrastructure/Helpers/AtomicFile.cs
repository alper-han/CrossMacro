
namespace CrossMacro.Infrastructure.Helpers;

/// <summary>Writes a file through a temporary sibling so readers never observe a partial file.</summary>
internal static class AtomicFile
{
    public static void Write(string filePath, Action<Stream> write)
    {
        EnsureParentDirectory(filePath);
        var temporaryPath = GetTemporaryPath(filePath);
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536))
            {
                write(stream);
                stream.Flush(flushToDisk: true);
            }

            Replace(filePath, temporaryPath);
        }
        finally
        {
            DeleteTemporaryFile(temporaryPath);
        }
    }

    public static async Task WriteAsync(string filePath, Func<Stream, Task> write, CancellationToken cancellationToken = default)
    {
        EnsureParentDirectory(filePath);
        var temporaryPath = GetTemporaryPath(filePath);
        try
        {
            var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
            await using (stream.ConfigureAwait(false))
            {
                await write(stream).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                RandomAccess.FlushToDisk(stream.SafeFileHandle);
            }

            Replace(filePath, temporaryPath);
        }
        finally
        {
            DeleteTemporaryFile(temporaryPath);
        }
    }

    private static void EnsureParentDirectory(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            _ = Directory.CreateDirectory(directory);
        }
    }

    private static string GetTemporaryPath(string filePath) => $"{filePath}.{Guid.NewGuid():N}.tmp";

    private static void Replace(string filePath, string temporaryPath)
    {
        if (File.Exists(filePath))
        {
            File.Replace(temporaryPath, filePath, destinationBackupFileName: null);
        }
        else
        {
            File.Move(temporaryPath, filePath);
        }
    }

    private static void DeleteTemporaryFile(string temporaryPath)
    {
        if (File.Exists(temporaryPath))
        {
            File.Delete(temporaryPath);
        }
    }
}
