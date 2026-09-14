using System;
using System.Diagnostics;
using System.ComponentModel;
using System.Security.Cryptography;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;

namespace CrossMacro.UI.Windows.Native;

internal static partial class WindowsNativeLibrariesBootstrapper
{
    private static readonly string[] NativeLibraryFileNames =
    [
        "libSkiaSharp.dll",
        "libHarfBuzzSharp.dll",
        "av_libglesv2.dll",
    ];

    private const string CompleteMarkerFileName = ".complete";

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16, EntryPoint = "SetDllDirectoryW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetDllDirectory(string lpPathName);

    [SupportedOSPlatform("windows")]
    public static void EnsureLoaded()
    {
        var assembly = typeof(WindowsNativeLibrariesBootstrapper).Assembly;
        var resourceNames = assembly.GetManifestResourceNames();

        var embeddedResources = resourceNames
            .Where(name => name.StartsWith("NativeRuntimes.", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (embeddedResources.Length is 0)
        {
            return;
        }

        try
        {
            var arch = GetRuntimeIdentifier(RuntimeInformation.ProcessArchitecture);
            var version = assembly.GetName().Version?.ToString() ?? "current";
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(localAppData))
            {
                throw new InvalidOperationException("A user-local application data directory is required for native runtime extraction.");
            }

            var targetDir = Path.Combine(localAppData, "CrossMacro", "runtimes", $"{version}-{arch}");
            var completeMarkerPath = Path.Combine(targetDir, CompleteMarkerFileName);

            if (File.Exists(completeMarkerPath) && NativeLibrariesMatchResources(targetDir))
            {
                SetNativeSearchDirectory(targetDir);
                return;
            }

            var mutexName = $@"Local\CrossMacro_NativeRuntime_{version}_{arch}";
            using var mutex = new Mutex(initiallyOwned: false, mutexName);
            var acquired = false;
            try
            {
                try
                {
                    acquired = mutex.WaitOne(TimeSpan.FromSeconds(30));
                }
                catch (AbandonedMutexException)
                {
                    acquired = true;
                }

                if (!acquired)
                {
                    throw new TimeoutException("Timed out waiting for native runtime initialization mutex.");
                }

                if (File.Exists(completeMarkerPath) && NativeLibrariesMatchResources(targetDir))
                {
                    SetNativeSearchDirectory(targetDir);
                    return;
                }

                _ = Directory.CreateDirectory(targetDir);

                foreach (var fileName in NativeLibraryFileNames)
                {
                    var compressedResourceName = embeddedResources.FirstOrDefault(r =>
                        r.EndsWith(fileName + ".gz", StringComparison.OrdinalIgnoreCase));
                    var rawResourceName = compressedResourceName is null
                        ? embeddedResources.FirstOrDefault(r => r.EndsWith(fileName, StringComparison.OrdinalIgnoreCase))
                        : null;

                    var resourceName = compressedResourceName ?? rawResourceName
                        ?? throw new InvalidDataException($"Missing embedded native library: {fileName}.");

                    var targetPath = Path.Combine(targetDir, fileName);
                    using var stream = assembly.GetManifestResourceStream(resourceName)
                        ?? throw new InvalidDataException($"Cannot open embedded native library: {fileName}.");

                    var tempPath = targetPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                    try
                    {
                        using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            if (compressedResourceName is not null)
                            {
                                using var gzipStream = new GZipStream(stream, CompressionMode.Decompress);
                                gzipStream.CopyTo(fileStream);
                            }
                            else
                            {
                                stream.CopyTo(fileStream);
                            }
                        }

                        File.Move(tempPath, targetPath, overwrite: true);
                    }
                    finally
                    {
                        if (File.Exists(tempPath))
                        {
                            try
                            {
                                File.Delete(tempPath);
                            }
                            catch (IOException cleanupError)
                            {
                                Trace.TraceWarning("Native extraction cleanup failed: {0}", cleanupError.Message);
                            }
                        }
                    }
                }

                if (!NativeLibrariesMatchResources(targetDir))
                {
                    throw new InvalidDataException("Extracted native libraries do not match the embedded resources.");
                }
                File.WriteAllText(completeMarkerPath, version);

                SetNativeSearchDirectory(targetDir);
            }
            finally
            {
                if (acquired)
                {
                    mutex.ReleaseMutex();
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or Win32Exception or TimeoutException or NotSupportedException)
        {
            Trace.TraceError("[WindowsNativeLibrariesBootstrapper] Native runtime setup failed: {0}", ex.Message);
            throw;
        }
    }

    internal static string GetRuntimeIdentifier(Architecture architecture)
    {
        if (architecture is Architecture.X64) { return "win-x64"; }
        if (architecture is Architecture.Arm64) { return "win-arm64"; }
        throw new PlatformNotSupportedException($"Embedded native libraries do not support {architecture}.");
    }

    [SupportedOSPlatform("windows")]
    private static void SetNativeSearchDirectory(string directory)
    {
        if (!SetDllDirectory(directory))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "Cannot configure the native library search directory.");
        }
    }

    internal static bool ContentMatches(Stream expected, Stream actual)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);
        return SHA256.HashData(expected).AsSpan().SequenceEqual(SHA256.HashData(actual));
    }

    private static bool NativeLibrariesMatchResources(string targetDir)
    {
        var assembly = typeof(WindowsNativeLibrariesBootstrapper).Assembly;
        var resources = assembly.GetManifestResourceNames();
        foreach (var fileName in NativeLibraryFileNames)
        {
            var path = Path.Combine(targetDir, fileName);
            if (!File.Exists(path)) { return false; }
            var compressed = resources.FirstOrDefault(name => name.StartsWith("NativeRuntimes.", StringComparison.Ordinal) && name.EndsWith(fileName + ".gz", StringComparison.Ordinal));
            var resource = compressed ?? resources.FirstOrDefault(name => name.StartsWith("NativeRuntimes.", StringComparison.Ordinal) && name.EndsWith(fileName, StringComparison.Ordinal));
            if (resource is null) { return false; }
            using var embedded = assembly.GetManifestResourceStream(resource);
            if (embedded is null) { return false; }
            using var actual = File.OpenRead(path);
            if (compressed is not null)
            {
                using var decompressed = new GZipStream(embedded, CompressionMode.Decompress);
                if (!ContentMatches(decompressed, actual)) { return false; }
            }
            else if (!ContentMatches(embedded, actual)) { return false; }
        }
        return true;
    }
}
