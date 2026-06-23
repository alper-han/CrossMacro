using CrossMacro.Platform.Linux.DisplayServer.Wayland;

namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading;

public sealed class KWinScreenShotCaptureTests
{
    [Fact]
    public void CreatePrivateRawFile_CreatesOwnerOnlyFileInsideOwnerOnlyDirectory()
    {
        var rawDirectory = Path.Combine(Path.GetTempPath(), $"crossmacro-kwin-screenshot-test-{Guid.NewGuid():N}");
        var rawPath = Path.Combine(rawDirectory, "frame.raw");

        try
        {
            KWinScreenShotCapture.CreatePrivateRawDirectory(rawDirectory);
            using (var file = KWinScreenShotCapture.CreatePrivateRawFile(rawPath))
            {
                file.WriteByte(1);

#pragma warning disable CA1416 // Linux platform test verifies Unix permissions.
                Assert.Equal(
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
                    File.GetUnixFileMode(rawDirectory));
                Assert.Equal(
                    UnixFileMode.UserRead | UnixFileMode.UserWrite,
                    File.GetUnixFileMode(rawPath));
#pragma warning restore CA1416
            }

            Assert.False(File.Exists(rawPath));
        }
        finally
        {
            if (Directory.Exists(rawDirectory))
            {
                Directory.Delete(rawDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void DuplicateForDbus_DoesNotCloseOriginalFileStreamHandle()
    {
        var rawDirectory = Path.Combine(Path.GetTempPath(), $"crossmacro-kwin-screenshot-test-{Guid.NewGuid():N}");
        var rawPath = Path.Combine(rawDirectory, "frame.raw");

        try
        {
            KWinScreenShotCapture.CreatePrivateRawDirectory(rawDirectory);
            using var file = KWinScreenShotCapture.CreatePrivateRawFile(rawPath);
            file.WriteByte(1);

            using (var duplicated = KWinScreenShotCapture.DuplicateForDbus(file.SafeFileHandle))
            {
                Assert.False(duplicated.IsInvalid);
            }

            file.Position = 0;
            Assert.Equal(1, file.ReadByte());
        }
        finally
        {
            if (Directory.Exists(rawDirectory))
            {
                Directory.Delete(rawDirectory, recursive: true);
            }
        }
    }
}
