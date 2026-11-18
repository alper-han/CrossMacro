using CrossMacro.UI.Windows.Native;

namespace CrossMacro.Platform.Windows.Tests;

public sealed class WindowsNativeLibrariesBootstrapperTests
{
    private static readonly string[] LibraryFileNames = ["libSkiaSharp.dll", "libHarfBuzzSharp.dll", "av_libglesv2.dll"];

    [Theory]
    [InlineData(System.Runtime.InteropServices.Architecture.X64, "win-x64")]
    [InlineData(System.Runtime.InteropServices.Architecture.Arm64, "win-arm64")]
    public void SupportedArchitecture_SelectsMatchingRuntime(System.Runtime.InteropServices.Architecture architecture, string expected)
    {
        Assert.Equal(expected, WindowsNativeLibrariesBootstrapper.GetRuntimeIdentifier(architecture));
    }

    [Fact]
    public void UnsupportedArchitecture_DoesNotFallBackToX64()
    {
        Assert.Throws<PlatformNotSupportedException>(() => WindowsNativeLibrariesBootstrapper.GetRuntimeIdentifier(System.Runtime.InteropServices.Architecture.X86));
    }

    [Fact]
    public void Extraction_ReplacesCorruptCacheWithExactResourceAndRemovesTemporaryFiles()
    {
        var directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"crossmacro-native-{Guid.NewGuid():N}");
        System.IO.Directory.CreateDirectory(directory);
        try
        {
            var target = System.IO.Path.Combine(directory, "library.dll");
            System.IO.File.WriteAllText(target, "corrupt cache");
            var expected = System.Text.Encoding.UTF8.GetBytes("resource content");
            using var source = new System.IO.MemoryStream();
            using (var gzip = new System.IO.Compression.GZipStream(source, System.IO.Compression.CompressionMode.Compress, leaveOpen: true))
            {
                gzip.Write(expected);
            }

            source.Position = 0;
            WindowsNativeLibrariesBootstrapper.ExtractLibrary(source, target);
            Assert.Equal(expected, System.IO.File.ReadAllBytes(target));
            Assert.Single(System.IO.Directory.GetFiles(directory));
        }
        finally { System.IO.Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public void InvalidCompressedResource_PreservesExistingCacheAndCleansTemporaryFile()
    {
        var directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"crossmacro-native-{Guid.NewGuid():N}");
        System.IO.Directory.CreateDirectory(directory);
        try
        {
            var target = System.IO.Path.Combine(directory, "library.dll");
            System.IO.File.WriteAllText(target, "previous");
            using var invalid = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes("invalid gzip header"));
            Assert.Throws<System.IO.InvalidDataException>(() => WindowsNativeLibrariesBootstrapper.ExtractLibrary(invalid, target));
            Assert.Equal("previous", System.IO.File.ReadAllText(target));
            Assert.Single(System.IO.Directory.GetFiles(directory));
        }
        finally { System.IO.Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public void IsCacheCurrent_RequiresMatchingMarkerAndEveryLibrary()
    {
        var directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"crossmacro-native-{Guid.NewGuid():N}");
        System.IO.Directory.CreateDirectory(directory);
        try
        {
            Assert.False(WindowsNativeLibrariesBootstrapper.IsCacheCurrent(directory, "build-a"));

            foreach (var fileName in LibraryFileNames)
            {
                System.IO.File.WriteAllText(System.IO.Path.Combine(directory, fileName), "library");
            }

            Assert.False(WindowsNativeLibrariesBootstrapper.IsCacheCurrent(directory, "build-a"));

            System.IO.File.WriteAllText(System.IO.Path.Combine(directory, ".complete"), "build-a");
            Assert.True(WindowsNativeLibrariesBootstrapper.IsCacheCurrent(directory, "build-a"));
            Assert.False(WindowsNativeLibrariesBootstrapper.IsCacheCurrent(directory, "build-b"));

            System.IO.File.Delete(System.IO.Path.Combine(directory, LibraryFileNames[1]));
            Assert.False(WindowsNativeLibrariesBootstrapper.IsCacheCurrent(directory, "build-a"));
        }
        finally { System.IO.Directory.Delete(directory, recursive: true); }
    }
}
