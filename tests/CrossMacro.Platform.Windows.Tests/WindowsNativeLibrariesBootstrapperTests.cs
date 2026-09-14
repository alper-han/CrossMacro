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
}
