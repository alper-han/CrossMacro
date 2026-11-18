using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Threading;

namespace CrossMacro.UI.Windows.Native;

/// <summary>
/// Portable builds embed the gzipped native Skia, HarfBuzz and ANGLE libraries. They are extracted once per
/// build into a per-user cache and the process loads them from there; a marker file holding the fingerprint of
/// the embedded resources makes every later start a file-existence check.
/// </summary>
internal static partial class WindowsNativeLibrariesBootstrapper
{
    private static readonly string[] NativeLibraryFileNames =
    [
        "libSkiaSharp.dll",
        "libHarfBuzzSharp.dll",
        "av_libglesv2.dll",
    ];

    private const string ResourcePrefix = "NativeRuntimes.";
    private const string CompleteMarkerFileName = ".complete";
    private static readonly TimeSpan ExtractionMutexTimeout = TimeSpan.FromSeconds(30);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16, EntryPoint = "SetDllDirectoryW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetDllDirectory(string lpPathName);

    [SupportedOSPlatform("windows")]
    public static void EnsureLoaded()
    {
        var assembly = typeof(WindowsNativeLibrariesBootstrapper).Assembly;
        var embeddedResources = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            .ToArray();

        if (embeddedResources.Length is 0)
        {
            return;
        }

        try
        {
            var libraries = ResolveEmbeddedLibraries(embeddedResources);
            var fingerprint = ComputeFingerprint(assembly, libraries);
            var arch = GetRuntimeIdentifier(RuntimeInformation.ProcessArchitecture);
            var version = assembly.GetName().Version?.ToString() ?? "current";
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(localAppData))
            {
                throw new InvalidOperationException("A user-local application data directory is required for native runtime extraction.");
            }

            var targetDir = Path.Combine(localAppData, "CrossMacro", "runtimes", $"{version}-{arch}");
            if (!IsCacheCurrent(targetDir, fingerprint))
            {
                ExtractUnderMutex(assembly, libraries, targetDir, fingerprint, $@"Local\CrossMacro_NativeRuntime_{version}_{arch}");
            }

            SetNativeSearchDirectory(targetDir);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or Win32Exception or TimeoutException or NotSupportedException or InvalidDataException)
        {
            Trace.TraceError("[WindowsNativeLibrariesBootstrapper] Native runtime setup failed: {0}", ex.Message);
            throw;
        }
    }

    /// <summary>Atomically replaces one cached library with the decompressed resource; the caller owns and serializes the cache directory.</summary>
    internal static void ExtractLibrary(Stream compressedStream, string targetPath)
    {
        ArgumentNullException.ThrowIfNull(compressedStream);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);
        var tempPath = targetPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var gzipStream = new GZipStream(compressedStream, CompressionMode.Decompress))
            {
                gzipStream.CopyTo(fileStream);
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

    internal static string GetRuntimeIdentifier(Architecture architecture)
    {
        if (architecture is Architecture.X64) { return "win-x64"; }
        if (architecture is Architecture.Arm64) { return "win-arm64"; }
        throw new PlatformNotSupportedException($"Embedded native libraries do not support {architecture}.");
    }

    /// <summary>True when the cache was completely extracted from the build identified by <paramref name="fingerprint"/>.</summary>
    internal static bool IsCacheCurrent(string targetDir, string fingerprint)
    {
        var markerPath = Path.Combine(targetDir, CompleteMarkerFileName);
        return File.Exists(markerPath)
            && string.Equals(File.ReadAllText(markerPath), fingerprint, StringComparison.Ordinal)
            && NativeLibraryFileNames.All(fileName => File.Exists(Path.Combine(targetDir, fileName)));
    }

    private static string[] ResolveEmbeddedLibraries(string[] embeddedResources)
    {
        return NativeLibraryFileNames
            .Select(fileName => embeddedResources.FirstOrDefault(name => name.EndsWith(fileName + ".gz", StringComparison.Ordinal))
                ?? throw new InvalidDataException($"Missing embedded native library: {fileName}."))
            .ToArray();
    }

    private static string ComputeFingerprint(Assembly assembly, string[] resourceNames)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var resourceName in resourceNames)
        {
            using var stream = OpenResource(assembly, resourceName);
            hash.AppendData(SHA256.HashData(stream));
        }

        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static void ExtractUnderMutex(Assembly assembly, string[] resourceNames, string targetDir, string fingerprint, string mutexName)
    {
        using var mutex = new Mutex(initiallyOwned: false, mutexName);
        var acquired = false;
        try
        {
            try
            {
                acquired = mutex.WaitOne(ExtractionMutexTimeout);
            }
            catch (AbandonedMutexException)
            {
                acquired = true;
            }

            if (!acquired)
            {
                throw new TimeoutException("Timed out waiting for native runtime initialization mutex.");
            }

            if (IsCacheCurrent(targetDir, fingerprint))
            {
                return;
            }

            _ = Directory.CreateDirectory(targetDir);
            for (var i = 0; i < NativeLibraryFileNames.Length; i++)
            {
                using var stream = OpenResource(assembly, resourceNames[i]);
                ExtractLibrary(stream, Path.Combine(targetDir, NativeLibraryFileNames[i]));
            }

            File.WriteAllText(Path.Combine(targetDir, CompleteMarkerFileName), fingerprint);
        }
        finally
        {
            if (acquired)
            {
                mutex.ReleaseMutex();
            }
        }
    }

    private static Stream OpenResource(Assembly assembly, string resourceName) =>
        assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidDataException($"Cannot open embedded native library resource: {resourceName}.");

    [SupportedOSPlatform("windows")]
    private static void SetNativeSearchDirectory(string directory)
    {
        if (!SetDllDirectory(directory))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "Cannot configure the native library search directory.");
        }
    }
}
