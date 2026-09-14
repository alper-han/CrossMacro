using CrossMacro.Infrastructure.Serialization;
using CrossMacro.Infrastructure.Helpers;
namespace CrossMacro.Infrastructure.Tests.Helpers;

public sealed class FileBackedJsonStorageTests
{
    [Fact]
    public async Task WriteAndRead_RoundTripsGeneratedJsonAndLeavesNoTemporaryFile()
    {
        var directory = Directory.CreateTempSubdirectory("crossmacro-json-storage-");
        try
        {
            var filePath = Path.Combine(directory.FullName, "nested", "settings.json");
            var expected = new HotkeySettings
            {
                RecordingHotkey = "Ctrl+Alt+R",
                PlaybackHotkey = "Ctrl+Alt+P",
            };

            FileBackedJsonStorage.Write(filePath, expected, CrossMacroJsonContext.Default.HotkeySettings);

            var actual = FileBackedJsonStorage.Read(filePath, CrossMacroJsonContext.Default.HotkeySettings);
            _ = actual.Should().NotBeNull();
            _ = actual.RecordingHotkey.Should().Be(expected.RecordingHotkey);
            _ = actual.PlaybackHotkey.Should().Be(expected.PlaybackHotkey);

            await FileBackedJsonStorage.WriteAsync(
                filePath,
                expected,
                CrossMacroJsonContext.Default.HotkeySettings,
                CancellationToken.None);

            var asyncActual = await FileBackedJsonStorage.ReadAsync(
                filePath,
                CrossMacroJsonContext.Default.HotkeySettings);
            _ = asyncActual.Should().NotBeNull();
            _ = asyncActual.RecordingHotkey.Should().Be(expected.RecordingHotkey);
            _ = asyncActual.PlaybackHotkey.Should().Be(expected.PlaybackHotkey);
            _ = Directory.EnumerateFiles(directory.FullName, "*.tmp", SearchOption.AllDirectories).Should().BeEmpty();
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
