using System.Runtime.InteropServices;

namespace CrossMacro.Platform.Linux.Native;

internal static class NativeLibraryLoader
{
    private const string ExtraPathVariable = "CROSSMACRO_NATIVE_LIBRARY_PATH";

    public static bool TryLoad(IReadOnlyList<string> names, out IntPtr handle)
    {
        foreach (var candidate in Candidates(names))
        {
            if (NativeLibrary.TryLoad(candidate, out handle))
            {
                return true;
            }
        }

        handle = IntPtr.Zero;
        return false;
    }

    public static IntPtr Load(IReadOnlyList<string> names, string displayName)
    {
        return TryLoad(names, out var handle)
            ? handle
            : throw new DllNotFoundException($"Unable to load {displayName}. Set LD_LIBRARY_PATH or {ExtraPathVariable} to the directory containing {names[0]}.");
    }

    private static IEnumerable<string> Candidates(IReadOnlyList<string> names)
    {
        foreach (var name in names)
        {
            yield return name;
        }

        foreach (var directory in ExtraDirectories())
        {
            foreach (var name in names)
            {
                yield return Path.Combine(directory, name);
            }
        }
    }

    private static IEnumerable<string> ExtraDirectories()
    {
        var raw = Environment.GetEnvironmentVariable(ExtraPathVariable);
        if (string.IsNullOrWhiteSpace(raw))
        {
            yield break;
        }

        foreach (var directory in raw.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Directory.Exists(directory))
            {
                yield return directory;
            }
        }
    }
}
