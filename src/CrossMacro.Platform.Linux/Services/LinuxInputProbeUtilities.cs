
namespace CrossMacro.Platform.Linux.Services;

internal static class LinuxInputProbeUtilities
{
    internal static string? ResolveAvailableSocketPath(Func<string, bool> fileExists)
    {
        ArgumentNullException.ThrowIfNull(fileExists);

        return fileExists(IpcProtocol.DefaultSocketPath)
            ? IpcProtocol.DefaultSocketPath
            : null;
    }

    internal static bool HasUInputWriteAccess(Func<string, bool> canOpenForWrite)
    {
        ArgumentNullException.ThrowIfNull(canOpenForWrite);

        return canOpenForWrite(LinuxSystemPaths.UInputDevicePath) ||
               canOpenForWrite(LinuxSystemPaths.UInputAlternatePath);
    }

    internal static bool CanOpenForWrite(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return false;
            }

            using (File.OpenWrite(path))
            {
                return true;
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return false;
        }
    }
}
