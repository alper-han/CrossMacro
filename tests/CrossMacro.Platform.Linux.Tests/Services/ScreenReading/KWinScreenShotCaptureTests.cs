
namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading;

public sealed class KWinScreenShotCaptureTests
{
    [Fact]
    public async Task CaptureAreaAsync_WhenCanceledBeforeStart_ShouldNotCreateRawDirectory()
    {
        var rawDirectory = Path.Combine(Path.GetTempPath(), $"crossmacro-kwin-screenshot-test-{Guid.NewGuid():N}");
        var capture = CreateCapture(rawDirectory);

        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        var result = await capture.CaptureAreaAsync(new ScreenRect(0, 0, 1, 1), new ScreenReadOptions(cancellationToken: cancellationSource.Token));

        Assert.Equal(ScreenReadErrorKind.Canceled, result.ErrorKind);
        Assert.False(Directory.Exists(rawDirectory));
    }

    [Fact]
    public async Task CaptureAreaAsync_WhenConnectionFails_ShouldDeleteRawDirectory()
    {
        var rawDirectory = Path.Combine(Path.GetTempPath(), $"crossmacro-kwin-screenshot-test-{Guid.NewGuid():N}");
        var capture = CreateCapture(rawDirectory);

        using var environmentScope = new EnvironmentVariableScope("DBUS_SESSION_BUS_ADDRESS", "unix:path=/tmp/crossmacro-does-not-exist");

        var result = await capture.CaptureAreaAsync(new ScreenRect(0, 0, 1, 1), new ScreenReadOptions());

        Assert.False(result.IsSuccess);
        Assert.False(Directory.Exists(rawDirectory));
    }

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

    private static KWinScreenShotCapture CreateCapture(string rawDirectory)
    {
        var environment = new LinuxEnvironmentSnapshot(
            FlatpakId: null,
            AppImage: null,
            UseDaemon: null,
            SessionType: null,
            WaylandDisplay: null,
            Display: null,
            CurrentDesktop: null,
            GdmSession: null,
            HyprlandInstanceSignature: null,
            RuntimeDir: null,
            WayfireSocket: null,
            SwaySocket: null,
            WindowButtons: null);

        return new KWinScreenShotCapture(environment, TimeProvider.System, () => rawDirectory);
    }

    private sealed class EnvironmentVariableScope : IDisposable
    {
        private readonly string _name;
        private readonly string? _previousValue;

        public EnvironmentVariableScope(string name, string? value)
        {
            _name = name;
            _previousValue = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(_name, _previousValue);
        }
    }
}
