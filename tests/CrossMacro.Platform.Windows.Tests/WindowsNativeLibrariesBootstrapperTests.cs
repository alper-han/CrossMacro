using CrossMacro.UI.Windows.Native;

namespace CrossMacro.Platform.Windows.Tests;

public sealed class WindowsNativeLibrariesBootstrapperTests
{
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

    [Theory]
    [InlineData("abcd", "abcd", true)]
    [InlineData("abcd", "abce", false)]
    [InlineData("abcd", "abc", false)]
    public void CacheValidation_ChecksContentRatherThanFileSize(string expected, string actual, bool matches)
    {
        using var embedded = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(expected));
        using var cached = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(actual));
        Assert.Equal(matches, WindowsNativeLibrariesBootstrapper.ContentMatches(embedded, cached));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Extraction_ReplacesCorruptCacheWithExactResourceAndRemovesTemporaryFiles(bool compressed)
    {
        var directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"crossmacro-native-{Guid.NewGuid():N}");
        System.IO.Directory.CreateDirectory(directory);
        try
        {
            var target = System.IO.Path.Combine(directory, "library.dll");
            System.IO.File.WriteAllText(target, "corrupt cache");
            var expected = System.Text.Encoding.UTF8.GetBytes("resource content");
            using var source = new System.IO.MemoryStream();
            if (compressed)
            {
                using var gzip = new System.IO.Compression.GZipStream(source, System.IO.Compression.CompressionMode.Compress, leaveOpen: true);
                gzip.Write(expected);
            }
            else { source.Write(expected); }
            source.Position = 0;
            WindowsNativeLibrariesBootstrapper.ExtractLibrary(source, target, compressed);
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
            Assert.Throws<System.IO.InvalidDataException>(() => WindowsNativeLibrariesBootstrapper.ExtractLibrary(invalid, target, compressed: true));
            Assert.Equal("previous", System.IO.File.ReadAllText(target));
            Assert.Single(System.IO.Directory.GetFiles(directory));
        }
        finally { System.IO.Directory.Delete(directory, recursive: true); }
    }

}
