
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public sealed class KWinScreenShotCapture : IKWinScreenShotCapture
{
    private const string Service = "org.kde.KWin.ScreenShot2";
    private const string Path = "/org/kde/KWin/ScreenShot2";
    private const string Interface = "org.kde.KWin.ScreenShot2";
    private const uint RawFormatBgra8888 = 6;
    private static readonly ScreenRect ProbeRegion = new(0, 0, 1, 1);

    private readonly bool _isAppImageKde;
    private readonly bool _isFlatpak;
    private readonly bool _isKde;
    private readonly TimeProvider _timeProvider;

    internal KWinScreenShotCapture()
        : this(LinuxEnvironmentVariables.CaptureCurrentSnapshot()) { /* Empty */ }

    public KWinScreenShotCapture(LinuxEnvironmentSnapshot environment)
        : this(environment, TimeProvider.System) { /* Empty */ }

    internal KWinScreenShotCapture(LinuxEnvironmentSnapshot environment, TimeProvider timeProvider)
    {
        _isAppImageKde = !string.IsNullOrEmpty(environment.AppImage) && IsKde(environment.CurrentDesktop);
        _isFlatpak = environment.IsFlatpak;
        _isKde = _isAppImageKde || IsKde(environment.CurrentDesktop);
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public KWinScreenShotSupportResult ProbeSupport()
    {
        if (_isAppImageKde)
        {
            EnsureAppImageKdeDesktopFile();
        }

        int maxRetries;
        if (_isFlatpak)
        {
            maxRetries = 1;
        }
        else if (_isAppImageKde)
        {
            maxRetries = 20;
        }
        else if (_isKde)
        {
            maxRetries = 6;
        }
        else
        {
            maxRetries = 1;
        }

        const int delayMs = 500;

        for (int i = 0; i < maxRetries; i++)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var result = CaptureAreaCoreAsync(ProbeRegion, new ScreenReadOptions(cancellationToken: cts.Token)).GetAwaiter().GetResult();

            if (result.IsSuccess)
            {
                return KWinScreenShotSupportResult.Supported();
            }

            if (_isKde && result.ErrorKind is not ScreenReadErrorKind.CaptureTimeout && i < maxRetries - 1)
            {
                Thread.Sleep(delayMs);
                continue;
            }

            return KWinScreenShotSupportResult.Failure(result.ErrorKind ?? ScreenReadErrorKind.BackendUnavailable, result.ErrorMessage ?? "KWin ScreenShot2 is unavailable.");
        }

        return KWinScreenShotSupportResult.Failure(ScreenReadErrorKind.BackendUnavailable, "KWin ScreenShot2 is unavailable.");
    }

    private static bool IsKde(string? currentDesktop)
    {
        return (currentDesktop?.Contains("KDE", StringComparison.OrdinalIgnoreCase)) is true;
    }

    private static void EnsureAppImageKdeDesktopFile()
    {
        var currentExe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(currentExe))
        {
            return;
        }

        try
        {
            var canonicalExe = File.ResolveLinkTarget("/proc/self/exe", returnFinalTarget: true)?.FullName ?? currentExe;
            var desktopDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "applications");
            var desktopFile = System.IO.Path.Combine(desktopDir, "crossmacro-appimage-kwin.desktop");

            _ = Directory.CreateDirectory(desktopDir);

            string desktopContent = $"[Desktop Entry]\nName=CrossMacro AppImage (Internal)\nExec={canonicalExe}\nType=Application\nNoDisplay=true\nX-KDE-DBUS-Restricted-Interfaces=org.kde.KWin.ScreenShot2\n";

            if (File.Exists(desktopFile) && File.ReadAllLines(desktopFile).Any(line => string.Equals(line, $"Exec={canonicalExe}", StringComparison.Ordinal)))
            {
                return;
            }

            File.WriteAllText(desktopFile, desktopContent);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Ignore desktop file generation failures
        }
    }

    public Task<KWinScreenShotCaptureResult> CaptureAreaAsync(ScreenRect region, ScreenReadOptions options)
    {
        if (options.CancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(KWinScreenShotCaptureResult.Failure(ScreenReadErrorKind.Canceled, "KWin ScreenShot2 capture was canceled before it started."));
        }

        if (region.Width <= 0 || region.Height <= 0)
        {
            return Task.FromResult(KWinScreenShotCaptureResult.Failure(ScreenReadErrorKind.OutOfBounds, $"Invalid KWin ScreenShot2 capture region {region}."));
        }

        return CaptureAreaCoreAsync(region, options);
    }

    public Task<KWinScreenShotCaptureResult> CaptureWorkspaceAsync(ScreenReadOptions options)
    {
        if (options.CancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(KWinScreenShotCaptureResult.Failure(ScreenReadErrorKind.Canceled, "KWin ScreenShot2 workspace capture was canceled before it started."));
        }

        return CaptureWorkspaceCoreAsync(options);
    }

    public void Dispose() { /* Empty */ }

    private async Task<KWinScreenShotCaptureResult> CaptureAreaCoreAsync(ScreenRect region, ScreenReadOptions options)
    {
        return await CaptureCoreAsync(
            options,
            "CaptureArea",
            "iiuua{sv}h",
            (ref MessageWriter writer) =>
            {
                writer.WriteInt32(region.X);
                writer.WriteInt32(region.Y);
                writer.WriteUInt32(checked((uint)region.Width));
                writer.WriteUInt32(checked((uint)region.Height));
            },
            rawCapture => CreateFrame(region, rawCapture)).ConfigureAwait(false);
    }

    private async Task<KWinScreenShotCaptureResult> CaptureWorkspaceCoreAsync(ScreenReadOptions options)
    {
        return await CaptureCoreAsync(
            options,
            "CaptureWorkspace",
            "a{sv}h",
            static (ref MessageWriter _) => { },
            CreateWorkspaceFrame).ConfigureAwait(false);
    }

    private async Task<KWinScreenShotCaptureResult> CaptureCoreAsync(
        ScreenReadOptions options,
        string method,
        string signature,
        MessageWriterAction writeArguments,
        Func<KWinRawCapture, KWinScreenShotFrame> frameFactory)
    {
        try
        {
            using var connection = new DBusConnection(DBusAddress.Session!);
            await connection.ConnectAsync().AsTask().WaitAsync(options.CancellationToken).ConfigureAwait(false);
            var rawCapture = await CaptureRawAsync(connection, options, method, signature, writeArguments).ConfigureAwait(false);
            var frame = frameFactory(rawCapture);
            return KWinScreenShotCaptureResult.Success(frame);
        }
        catch (OperationCanceledException)
        {
            return KWinScreenShotCaptureResult.Failure(ScreenReadErrorKind.Canceled, "KWin ScreenShot2 capture was canceled.");
        }
        catch (TimeoutException ex)
        {
            return KWinScreenShotCaptureResult.Failure(ScreenReadErrorKind.CaptureTimeout, ex.Message);
        }
        catch (Exception ex) when (ex is InvalidOperationException or DBusErrorReplyException or DBusConnectFailedException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            return KWinScreenShotCaptureResult.Failure(MapException(ex), BuildErrorMessage(ex));
        }
    }

    private async Task<KWinRawCapture> CaptureRawAsync(
        DBusConnection connection,
        ScreenReadOptions options,
        string method,
        string signature,
        MessageWriterAction writeArguments)
    {
        var pipe = new KWinScreenShotPipe();
        try
        {
            Task<Dictionary<string, VariantValue>> call;
            using (var dbusHandle = DuplicateForDbus(pipe.WriteHandle))
            {
                var writer = connection.GetMessageWriter();
                writer.WriteMethodCallHeader(Service, Path, Interface, method, signature);
                writeArguments(ref writer);
                writer.WriteDictionary(Array.Empty<KeyValuePair<string, VariantValue>>());
                writer.WriteHandle(dbusHandle);

                call = connection.CallMethodAsync(writer.CreateMessage(), static (message, _) =>
                {
                    var reader = message.GetBodyReader();
                    return reader.ReadDictionaryOfStringToVariantValue();
                });
            }

            pipe.WriteHandle.Dispose();

            var results = options.Timeout is { } timeout
                ? await call.WaitAsync(timeout, _timeProvider, options.CancellationToken).ConfigureAwait(false)
                : await call.WaitAsync(options.CancellationToken).ConfigureAwait(false);

            var pixels = await ReadCapturedBytesAsync(pipe.ReadStream, results, options).ConfigureAwait(false);
            return new KWinRawCapture(results, pixels);
        }
        finally
        {
            await pipe.DisposeAsync().ConfigureAwait(false);
        }
    }

    internal static SafeFileHandle DuplicateForDbus(SafeHandle fileHandle)
    {
        var duplicated = PortalPipeWireLibc.dup(fileHandle);
        if (duplicated < 0)
        {
            throw new InvalidOperationException($"dup(KWin ScreenShot2 fd) failed errno={Marshal.GetLastPInvokeError().ToString(CultureInfo.InvariantCulture)}.");
        }

        return new SafeFileHandle(new IntPtr(duplicated), ownsHandle: true);
    }

    private static KWinScreenShotFrame CreateFrame(ScreenRect region, KWinRawCapture rawCapture)
    {
        var results = rawCapture.Results;
        var width = GetRequiredUInt(results, "width");
        var height = GetRequiredUInt(results, "height");
        var stride = GetRequiredUInt(results, "stride");
        var format = GetRequiredUInt(results, "format");
        var type = results.TryGetValue("type", out var typeValue) ? typeValue.GetString() : "raw";

        if (width != region.Width || height != region.Height)
        {
            throw new InvalidOperationException($"KWin ScreenShot2 returned {width.ToString(CultureInfo.InvariantCulture)}x{height.ToString(CultureInfo.InvariantCulture)} for requested region {region}.");
        }

        if (!string.Equals(type, "raw", StringComparison.Ordinal) || format != RawFormatBgra8888)
        {
            throw new InvalidOperationException($"KWin ScreenShot2 returned unsupported image type='{type}' format={format.ToString(CultureInfo.InvariantCulture)}.");
        }

        return new KWinScreenShotFrame(region, checked((int)stride), ScreenPixelFormat.Bgra8888, rawCapture.Pixels);
    }

    private static KWinScreenShotFrame CreateWorkspaceFrame(KWinRawCapture rawCapture)
    {
        var results = rawCapture.Results;
        var width = GetRequiredUInt(results, "width");
        var height = GetRequiredUInt(results, "height");
        var region = new ScreenRect(0, 0, checked((int)width), checked((int)height));
        return CreateFrame(region, rawCapture);
    }

    internal static async Task<byte[]> ReadCapturedBytesAsync(
        Stream stream,
        IReadOnlyDictionary<string, VariantValue> results,
        ScreenReadOptions options)
    {
        var stride = GetRequiredUInt(results, "stride");
        var height = GetRequiredUInt(results, "height");
        var expectedLength = checked((ulong)stride * height);
        if (expectedLength > int.MaxValue)
        {
            throw new InvalidOperationException("KWin ScreenShot2 returned a raw frame that is too large for the supported pixel buffer.");
        }

        var expectedByteCount = (int)expectedLength;
        var pixels = new byte[expectedByteCount];
        var offset = 0;
        while (offset < pixels.Length)
        {
            var read = await stream.ReadAsync(pixels.AsMemory(offset, pixels.Length - offset), options.CancellationToken).ConfigureAwait(false);
            if (read is 0)
            {
                throw new EndOfStreamException($"KWin ScreenShot2 raw frame ended after {offset.ToString(CultureInfo.InvariantCulture)} of {expectedByteCount.ToString(CultureInfo.InvariantCulture)} bytes.");
            }

            offset += read;
        }

        return pixels;
    }

    private static uint GetRequiredUInt(IReadOnlyDictionary<string, VariantValue> results, string key)
    {
        if (!results.TryGetValue(key, out var value))
        {
            throw new InvalidOperationException($"KWin ScreenShot2 response did not include '{key}'.");
        }

        return value.GetUInt32();
    }

    private static ScreenReadErrorKind MapException(Exception ex)
    {
        if (ex is DBusErrorReplyException dbus && (dbus.ErrorName.Contains("NoAuthorized", StringComparison.OrdinalIgnoreCase) || dbus.ErrorName.Contains("AccessDenied", StringComparison.OrdinalIgnoreCase) || (dbus.ErrorMessage?.Contains("Not authorized", StringComparison.OrdinalIgnoreCase)) is true))
        {
            return ScreenReadErrorKind.PermissionDenied;
        }

        return ex is TimeoutException ? ScreenReadErrorKind.CaptureTimeout : ScreenReadErrorKind.CaptureFailed;
    }

    private static string BuildErrorMessage(Exception ex)
    {
        if (ex is DBusErrorReplyException dbus && (dbus.ErrorName.Contains("NoAuthorized", StringComparison.OrdinalIgnoreCase) || dbus.ErrorName.Contains("AccessDenied", StringComparison.OrdinalIgnoreCase) || (dbus.ErrorMessage?.Contains("Not authorized", StringComparison.OrdinalIgnoreCase)) is true))
        {
            return "KWin ScreenShot2 permission denied. Install a desktop entry for CrossMacro that includes X-KDE-DBUS-Restricted-Interfaces=org.kde.KWin.ScreenShot2.";
        }

        return ex.Message;
    }

    private readonly record struct KWinRawCapture(IReadOnlyDictionary<string, VariantValue> Results, byte[] Pixels);
}
