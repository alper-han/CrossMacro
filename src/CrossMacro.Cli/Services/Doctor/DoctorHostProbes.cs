namespace CrossMacro.Cli.Services.Doctor;

/// <summary>The operating-system facts the doctor reads, grouped so tests can supply a fake host.</summary>
internal sealed record DoctorHostProbes(
    Func<string, string?> GetEnvironmentVariable,
    Func<string, bool> FileExists,
    Func<string, bool> CanOpenForWrite,
    Func<bool> HasUsableReadableInputDevices,
    Func<string, string?> ReadAllTextIfExists,
    Func<bool> IsLinux,
    Func<bool> IsMacOS)
{
    public static DoctorHostProbes Current { get; } = new(
        Environment.GetEnvironmentVariable,
        File.Exists,
        CanOpenForWriteOnDisk,
        () => GetInputEventCandidates().Any(CanOpenForReadOnDisk),
        ReadAllTextIfExistsOnDisk,
        OperatingSystem.IsLinux,
        OperatingSystem.IsMacOS);

    private static bool CanOpenForWriteOnDisk(string path)
    {
        try
        {
            using var stream = File.Open(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
            return true;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return false;
        }
    }

    private static bool CanOpenForReadOnDisk(string path)
    {
        try
        {
            using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return true;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return false;
        }
    }

    private static string[] GetInputEventCandidates()
    {
        try
        {
            return Directory.Exists("/dev/input")
                ? Directory.GetFiles("/dev/input", "event*")
                : [];
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return [];
        }
    }

    private static string? ReadAllTextIfExistsOnDisk(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return null;
        }
    }
}
