using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;

namespace CrossMacro.UI.Windows.Native;

[SupportedOSPlatform("windows")]
internal static partial class WindowsNativeLibrariesBootstrapper
{
    private static readonly string[] NativeLibraryFileNames =
    [
        "libSkiaSharp.dll",
        "libHarfBuzzSharp.dll",
        "av_libglesv2.dll",
    ];

    private const string CompleteMarkerFileName = ".complete";

    [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16, EntryPoint = "SetDllDirectoryW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetDllDirectory(string lpPathName);

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Bootstrap failure is non-fatal; OS loader fallback is preferred")]
    [SuppressMessage("Security", "S5443:Using publicly writable directories is security-sensitive", Justification = "LocalApplicationData is user-scoped; fallback to user profile or temp if unavailable")]
    [SuppressMessage("Style", "IDE0072:Add missing cases to switch expression", Justification = "Only Windows desktop x64 and arm64 architectures are relevant")]
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
            var arch = RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.Arm64 => "win-arm64",
                Architecture.X64 => "win-x64",
                _ => "win-x64",
            };

            var version = assembly.GetName().Version?.ToString() ?? "current";
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(localAppData))
            {
                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                localAppData = !string.IsNullOrWhiteSpace(userProfile)
                    ? Path.Combine(userProfile, "AppData", "Local")
                    : Path.GetTempPath();
            }

            var targetDir = Path.Combine(localAppData, "CrossMacro", "runtimes", $"{version}-{arch}");
            var completeMarkerPath = Path.Combine(targetDir, CompleteMarkerFileName);

            if (File.Exists(completeMarkerPath) && AllNativeLibrariesExist(targetDir))
            {
                _ = SetDllDirectory(targetDir);
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
                    Trace.TraceWarning("[WindowsNativeLibrariesBootstrapper] Timed out waiting for native runtime initialization mutex.");
                    return;
                }

                if (File.Exists(completeMarkerPath) && AllNativeLibrariesExist(targetDir))
                {
                    _ = SetDllDirectory(targetDir);
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

                    var resourceName = compressedResourceName ?? rawResourceName;
                    if (resourceName is null)
                    {
                        continue;
                    }

                    var targetPath = Path.Combine(targetDir, fileName);
                    using var stream = assembly.GetManifestResourceStream(resourceName);
                    if (stream is null)
                    {
                        continue;
                    }

                    var tempPath = targetPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
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

                    try
                    {
                        File.Move(tempPath, targetPath, overwrite: true);
                    }
                    catch (IOException) when (File.Exists(targetPath))
                    {
                        _ = targetPath;
                    }
                    finally
                    {
                        if (File.Exists(tempPath))
                        {
                            try
                            {
                                File.Delete(tempPath);
                            }
                            catch (IOException)
                            {
                                _ = tempPath;
                            }
                        }
                    }
                }

                try
                {
                    File.WriteAllText(completeMarkerPath, DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
                }
                catch (IOException)
                {
                    _ = completeMarkerPath;
                }

                _ = SetDllDirectory(targetDir);
            }
            finally
            {
                if (acquired)
                {
                    mutex.ReleaseMutex();
                }
            }
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("[WindowsNativeLibrariesBootstrapper] Native runtime setup failed: {0}", ex.Message);
        }
    }

    private static bool AllNativeLibrariesExist(string targetDir)
    {
        foreach (var fileName in NativeLibraryFileNames)
        {
            var path = Path.Combine(targetDir, fileName);
            if (!File.Exists(path) || new FileInfo(path).Length is 0)
            {
                return false;
            }
        }

        return true;
    }
}
