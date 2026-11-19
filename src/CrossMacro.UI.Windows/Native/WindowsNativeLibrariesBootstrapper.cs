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
/// Portable builds embed the gzipped native Skia, HarfBuzz and ANGLE libraries. They are extracted into a
/// per-user cache and the process loads them from there. The cache is writable by the user, so every start
/// compares each cached library with its embedded resource and extracts again when one is missing or differs.
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
            var resources = ResolveEmbeddedLibraries(embeddedResources);
            Stream OpenLibrary(string fileName) => OpenResource(assembly, resources[fileName]);
            var arch = GetRuntimeIdentifier(RuntimeInformation.ProcessArchitecture);
            var version = assembly.GetName().Version?.ToString() ?? "current";
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(localAppData))
            {
                throw new InvalidOperationException("A user-local application data directory is required for native runtime extraction.");
            }

            var targetDir = Path.Combine(localAppData, "CrossMacro", "runtimes", $"{version}-{arch}");
            if (!IsCacheCurrent(targetDir, OpenLibrary))
            {
                ExtractUnderMutex(OpenLibrary, targetDir, $@"Local\CrossMacro_NativeRuntime_{version}_{arch}");
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

    /// <summary>True when every cached library exists and equals the embedded resource it was extracted from.</summary>
    internal static bool IsCacheCurrent(string targetDir, Func<string, Stream> openCompressedLibrary)
    {
        ArgumentNullException.ThrowIfNull(openCompressedLibrary);
        return NativeLibraryFileNames.All(fileName => LibraryMatches(Path.Combine(targetDir, fileName), () => openCompressedLibrary(fileName)));
    }

    private static bool LibraryMatches(string path, Func<Stream> openCompressedLibrary)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        using var compressed = openCompressedLibrary();
        using var expected = new GZipStream(compressed, CompressionMode.Decompress);
        using var actual = File.OpenRead(path);
        return SHA256.HashData(expected).AsSpan().SequenceEqual(SHA256.HashData(actual));
    }

    private static Dictionary<string, string> ResolveEmbeddedLibraries(string[] embeddedResources)
    {
        return NativeLibraryFileNames.ToDictionary(
            fileName => fileName,
            fileName => embeddedResources.FirstOrDefault(name => name.EndsWith(fileName + ".gz", StringComparison.Ordinal))
                ?? throw new InvalidDataException($"Missing embedded native library: {fileName}."),
            StringComparer.Ordinal);
    }

    private static void ExtractUnderMutex(Func<string, Stream> openCompressedLibrary, string targetDir, string mutexName)
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

            if (IsCacheCurrent(targetDir, openCompressedLibrary))
            {
                return;
            }

            _ = Directory.CreateDirectory(targetDir);
            foreach (var fileName in NativeLibraryFileNames)
            {
                using var stream = openCompressedLibrary(fileName);
                ExtractLibrary(stream, Path.Combine(targetDir, fileName));
            }

            if (!IsCacheCurrent(targetDir, openCompressedLibrary))
            {
                throw new InvalidDataException("Extracted native libraries do not match the embedded resources.");
            }
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
