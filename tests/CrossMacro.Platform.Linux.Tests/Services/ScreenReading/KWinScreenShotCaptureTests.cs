
namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading;

public sealed class KWinScreenShotCaptureTests
{
    [Fact]
    public void ProbeSupport_OnKdeX11_ReturnsWithoutProbingKWinScreenShot2()
    {
        var environment = default(LinuxEnvironmentSnapshot) with
        {
            SessionType = "x11",
            Display = ":0",
            CurrentDesktop = "KDE",
        };
        var capture = new KWinScreenShotCapture(environment, TimeProvider.System);

        var result = capture.ProbeSupport();

        Assert.False(result.IsSupported);
        Assert.Contains("KDE Wayland", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CaptureAreaAsync_WhenCanceledBeforeStart_ShouldReturnCanceled()
    {
        var capture = CreateCapture();

        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        var result = await capture.CaptureAreaAsync(new ScreenRect(0, 0, 1, 1), new ScreenReadOptions(cancellationToken: cancellationSource.Token));

        Assert.Equal(ScreenReadErrorKind.Canceled, result.ErrorKind);
    }

    [Fact]
    public async Task CaptureAreaAsync_WhenConnectionFails_ShouldReturnFailure()
    {
        var capture = CreateCapture();

        using var environmentScope = new EnvironmentVariableScope("DBUS_SESSION_BUS_ADDRESS", "unix:path=/tmp/crossmacro-does-not-exist");

        var result = await capture.CaptureAreaAsync(new ScreenRect(0, 0, 1, 1), new ScreenReadOptions());

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void DuplicateForDbus_DoesNotCloseOriginalFileStreamHandle()
    {
        var rawDirectory = Path.Combine(Path.GetTempPath(), $"crossmacro-kwin-screenshot-test-{Guid.NewGuid():N}");
        var rawPath = Path.Combine(rawDirectory, "frame.raw");

        try
        {
            Directory.CreateDirectory(rawDirectory);
            using var file = new FileStream(rawPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
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

    [Fact]
    public async Task ReadCapturedBytesAsync_WhenWriterFinishesAfterDbusReply_ShouldWaitForDeclaredFrameSize()
    {
        var pixels = Enumerable.Range(0, 16).Select(value => (byte)value).ToArray();
        var results = new Dictionary<string, VariantValue>(StringComparer.Ordinal)
        {
            ["stride"] = VariantValue.UInt32(8),
            ["height"] = VariantValue.UInt32(2),
        };

        var actual = await KWinScreenShotCapture.ReadCapturedBytesAsync(
            new DelayedChunkReadStream(pixels, TimeSpan.FromMilliseconds(50), chunkSize: 3),
            results,
            new ScreenReadOptions());

        Assert.Equal(pixels, actual);
    }

    [Fact]
    public async Task KWinScreenShotPipe_WhenNativeWriterWritesPixels_ShouldReadAllPixelsAsync()
    {
        var pixels = Enumerable.Range(0, 16).Select(value => (byte)(value + 10)).ToArray();
        var results = new Dictionary<string, VariantValue>(StringComparer.Ordinal)
        {
            ["stride"] = VariantValue.UInt32(8),
            ["height"] = VariantValue.UInt32(2),
        };

        await using var pipe = new KWinScreenShotPipe();
        using var duplicatedWriteHandle = KWinScreenShotCapture.DuplicateForDbus(pipe.WriteHandle);
        pipe.WriteHandle.Dispose();

        var writeTask = Task.Run(() => WriteNative(duplicatedWriteHandle, pixels));
        var actual = await KWinScreenShotCapture.ReadCapturedBytesAsync(
            pipe.ReadStream,
            results,
            new ScreenReadOptions());

        await writeTask;

        Assert.Equal(pixels, actual);
    }

    private static KWinScreenShotCapture CreateCapture()
    {
        var environment = new LinuxEnvironmentSnapshot(
            FlatpakId: null,
            AppImage: null,
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

        return new KWinScreenShotCapture(environment, TimeProvider.System);
    }

    private static void WriteNative(SafeFileHandle handle, byte[] pixels)
    {
        var written = NativeWrite(handle.DangerousGetHandle().ToInt32(), pixels, (nuint)pixels.Length);
        if (written != pixels.Length)
        {
            throw new IOException($"native pipe write returned {written} bytes.");
        }
    }

    [DllImport("libc.so.6", EntryPoint = "write", SetLastError = true)]
    private static extern nint NativeWrite(int fileDescriptor, byte[] buffer, nuint count);

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

    private sealed class DelayedChunkReadStream(byte[] data, TimeSpan delay, int chunkSize) : Stream
    {
        private int _offset;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => data.Length;

        public override long Position
        {
            get => _offset;
            set => throw new NotSupportedException();
        }

        public override void Flush() => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return ReadChunkAsync(buffer, cancellationToken);
        }

        private async ValueTask<int> ReadChunkAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            await Task.Delay(delay, TimeProvider.System, cancellationToken);
            var count = Math.Min(Math.Min(chunkSize, buffer.Length), data.Length - _offset);
            if (count is 0)
            {
                return 0;
            }

            data.AsMemory(_offset, count).CopyTo(buffer);
            _offset += count;
            return count;
        }
    }
}
