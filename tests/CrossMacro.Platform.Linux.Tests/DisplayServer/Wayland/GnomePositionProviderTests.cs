namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;

using System;
using System.IO;
using System.Threading.Tasks;
using CrossMacro.Platform.Linux.DisplayServer.Wayland;

public class GnomePositionProviderTests
{
    [Fact]
    public async Task EnsureFileContentAsync_ShouldCreateFile_WhenMissing()
    {
        using var tempDir = new TempDirectory();
        var filePath = Path.Combine(tempDir.Path, "extension.js");
        const string expectedContent = "new-content";

        var changed = await GnomePositionProvider.EnsureFileContentAsync(filePath, expectedContent);

        Assert.True(changed);
        Assert.True(File.Exists(filePath));
        Assert.Equal(expectedContent, await File.ReadAllTextAsync(filePath));
    }

    [Fact]
    public async Task EnsureFileContentAsync_ShouldNotRewrite_WhenContentMatches()
    {
        using var tempDir = new TempDirectory();
        var filePath = Path.Combine(tempDir.Path, "extension.js");
        const string expectedContent = "same-content";
        await File.WriteAllTextAsync(filePath, expectedContent);

        var changed = await GnomePositionProvider.EnsureFileContentAsync(filePath, expectedContent);

        Assert.False(changed);
        Assert.Equal(expectedContent, await File.ReadAllTextAsync(filePath));
    }

    [Fact]
    public async Task EnsureFileContentAsync_ShouldRewrite_WhenContentDiffers()
    {
        using var tempDir = new TempDirectory();
        var filePath = Path.Combine(tempDir.Path, "extension.js");
        await File.WriteAllTextAsync(filePath, "old-content");
        const string expectedContent = "updated-content";

        var changed = await GnomePositionProvider.EnsureFileContentAsync(filePath, expectedContent);

        Assert.True(changed);
        Assert.Equal(expectedContent, await File.ReadAllTextAsync(filePath));
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"crossmacro-tests-{Guid.NewGuid():N}");

        public TempDirectory()
        {
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
