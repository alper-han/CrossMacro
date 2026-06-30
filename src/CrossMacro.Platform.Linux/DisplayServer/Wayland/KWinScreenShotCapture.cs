using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.Linux.DisplayServer.Wayland.PortalPipeWire;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using Tmds.DBus.Protocol;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public sealed class KWinScreenShotCapture : IKWinScreenShotCapture
{
    private const string Service = "org.kde.KWin.ScreenShot2";
    private const string Path = "/org/kde/KWin/ScreenShot2";
    private const string Interface = "org.kde.KWin.ScreenShot2";
    private const uint RawFormatBgra8888 = 6;
    private const UnixFileMode OwnerOnlyDirectoryMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private const UnixFileMode OwnerOnlyFileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private static readonly ScreenRect ProbeRegion = new(0, 0, 1, 1);

    public KWinScreenShotSupportResult ProbeSupport()
    {
        bool isAppImageKde = IsAppImageKdeEnvironment();
        if (isAppImageKde)
        {
            EnsureAppImageKdeDesktopFile();
        }

        bool isFlatpak = File.Exists("/.flatpak-info");
        bool isKde = isAppImageKde || IsKdeEnvironment();
        
        int maxRetries;
        if (isFlatpak) maxRetries = 1;
        else if (isAppImageKde) maxRetries = 20;
        else if (isKde) maxRetries = 6;
        else maxRetries = 1;
        
        int delayMs = 500;

        for (int i = 0; i < maxRetries; i++)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var result = CaptureAreaCoreAsync(ProbeRegion, new ScreenReadOptions(cancellationToken: cts.Token)).GetAwaiter().GetResult();
            
            if (result.IsSuccess)
            {
                return KWinScreenShotSupportResult.Supported();
            }

            if (isKde && result.ErrorKind != ScreenReadErrorKind.CaptureTimeout && i < maxRetries - 1)
            {
                Thread.Sleep(delayMs);
                continue;
            }

            return KWinScreenShotSupportResult.Failure(result.ErrorKind ?? ScreenReadErrorKind.BackendUnavailable, result.ErrorMessage ?? "KWin ScreenShot2 is unavailable.");
        }
        
        return KWinScreenShotSupportResult.Failure(ScreenReadErrorKind.BackendUnavailable, "KWin ScreenShot2 is unavailable.");
    }

    private static bool IsKdeEnvironment()
    {
        return Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP")?.Contains("KDE", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool IsAppImageKdeEnvironment()
    {
        var appImage = Environment.GetEnvironmentVariable("APPIMAGE");
        var isKde = Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP")?.Contains("KDE", StringComparison.OrdinalIgnoreCase) == true;
        return !string.IsNullOrEmpty(appImage) && isKde;
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
            var canonicalExe = File.ResolveLinkTarget("/proc/self/exe", true)?.FullName ?? currentExe;
            var desktopDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "applications");
            var desktopFile = System.IO.Path.Combine(desktopDir, "crossmacro-appimage-kwin.desktop");

            Directory.CreateDirectory(desktopDir);

            string desktopContent = $"""
[Desktop Entry]
Name=CrossMacro AppImage (Internal)
Exec={canonicalExe}
Type=Application
NoDisplay=true
X-KDE-DBUS-Restricted-Interfaces=org.kde.KWin.ScreenShot2
""";

            if (File.Exists(desktopFile))
            {
                var existingLines = File.ReadAllLines(desktopFile);
                foreach (var line in existingLines)
                {
                    if (string.Equals(line, $"Exec={canonicalExe}", StringComparison.Ordinal))
                    {
                        return;
                    }
                }
            }

            File.WriteAllText(desktopFile, desktopContent);
        }
        catch
        {
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

    public void Dispose()
    {
    }

    private static async Task<KWinScreenShotCaptureResult> CaptureAreaCoreAsync(ScreenRect region, ScreenReadOptions options)
    {
        var rawDirectory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"crossmacro-kwin-screenshot-{Guid.NewGuid():N}");
        var rawPath = System.IO.Path.Combine(rawDirectory, "frame.raw");
        try
        {
            CreatePrivateRawDirectory(rawDirectory);
            using var connection = new DBusConnection(DBusAddress.Session!);
            await connection.ConnectAsync().AsTask().WaitAsync(options.CancellationToken).ConfigureAwait(false);
            var rawCapture = await CaptureRawAsync(connection, region, rawPath, options).ConfigureAwait(false);
            var frame = CreateFrame(region, rawCapture);
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
        catch (Exception ex) when (ex is InvalidOperationException or DBusErrorReplyException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            return KWinScreenShotCaptureResult.Failure(MapException(ex), BuildErrorMessage(ex));
        }
        finally
        {
            TryDelete(rawPath);
            TryDeleteDirectory(rawDirectory);
        }
    }

    private static async Task<KWinRawCapture> CaptureRawAsync(
        DBusConnection connection,
        ScreenRect region,
        string rawPath,
        ScreenReadOptions options)
    {
        await using var file = CreatePrivateRawFile(rawPath);
        using var dbusHandle = DuplicateForDbus(file.SafeFileHandle);
        var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(Service, Path, Interface, "CaptureArea", "iiuua{sv}h");
        writer.WriteInt32(region.X);
        writer.WriteInt32(region.Y);
        writer.WriteUInt32(checked((uint)region.Width));
        writer.WriteUInt32(checked((uint)region.Height));
        writer.WriteDictionary(Array.Empty<KeyValuePair<string, VariantValue>>());
        writer.WriteHandle(dbusHandle);

        var call = connection.CallMethodAsync(writer.CreateMessage(), static (message, _) =>
        {
            var reader = message.GetBodyReader();
            return reader.ReadDictionaryOfStringToVariantValue();
        });

        var results = options.Timeout is { } timeout
            ? await call.WaitAsync(timeout, options.CancellationToken).ConfigureAwait(false)
            : await call.WaitAsync(options.CancellationToken).ConfigureAwait(false);

        var pixels = ReadCapturedBytes(file);
        return new KWinRawCapture(results, pixels);
    }

    internal static SafeFileHandle DuplicateForDbus(SafeFileHandle fileHandle)
    {
        var duplicated = PortalPipeWireLibc.dup((int)fileHandle.DangerousGetHandle());
        if (duplicated < 0)
        {
            throw new InvalidOperationException($"dup(KWin ScreenShot2 fd) failed errno={Marshal.GetLastPInvokeError()}.");
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
            throw new InvalidOperationException($"KWin ScreenShot2 returned {width}x{height} for requested region {region}.");
        }

        if (!string.Equals(type, "raw", StringComparison.Ordinal) || format != RawFormatBgra8888)
        {
            throw new InvalidOperationException($"KWin ScreenShot2 returned unsupported image type='{type}' format={format}.");
        }

        return new KWinScreenShotFrame(region, checked((int)stride), ScreenPixelFormat.Bgra8888, rawCapture.Pixels);
    }

    internal static void CreatePrivateRawDirectory(string rawDirectory)
    {
        Directory.CreateDirectory(rawDirectory);
#pragma warning disable CA1416 // CrossMacro.Platform.Linux runs on Linux; this secures KWin raw capture files.
        File.SetUnixFileMode(rawDirectory, OwnerOnlyDirectoryMode);
#pragma warning restore CA1416
    }

    internal static FileStream CreatePrivateRawFile(string rawPath)
    {
#pragma warning disable CA1416 // CrossMacro.Platform.Linux runs on Linux; this creates owner-only KWin raw capture files.
        var options = new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.ReadWrite,
            Share = FileShare.None,
            Options = FileOptions.DeleteOnClose,
            UnixCreateMode = OwnerOnlyFileMode
        };

        return new FileStream(rawPath, options);
#pragma warning restore CA1416
    }

    private static byte[] ReadCapturedBytes(FileStream file)
    {
        file.Flush();
        file.Position = 0;
        var length = checked((int)file.Length);
        var pixels = new byte[length];
        var offset = 0;
        while (offset < pixels.Length)
        {
            var read = file.Read(pixels, offset, pixels.Length - offset);
            if (read == 0)
            {
                break;
            }

            offset += read;
        }

        if (offset != pixels.Length)
        {
            throw new EndOfStreamException("KWin ScreenShot2 raw file changed while it was being read.");
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
        if (ex is DBusErrorReplyException dbus && 
            (dbus.ErrorName.Contains("NoAuthorized", StringComparison.OrdinalIgnoreCase) || 
             dbus.ErrorName.Contains("AccessDenied", StringComparison.OrdinalIgnoreCase) ||
             dbus.ErrorMessage?.Contains("Not authorized", StringComparison.OrdinalIgnoreCase) == true))
        {
            return ScreenReadErrorKind.PermissionDenied;
        }

        return ex is TimeoutException ? ScreenReadErrorKind.CaptureTimeout : ScreenReadErrorKind.CaptureFailed;
    }

    private static string BuildErrorMessage(Exception ex)
    {
        if (ex is DBusErrorReplyException dbus && 
            (dbus.ErrorName.Contains("NoAuthorized", StringComparison.OrdinalIgnoreCase) || 
             dbus.ErrorName.Contains("AccessDenied", StringComparison.OrdinalIgnoreCase) ||
             dbus.ErrorMessage?.Contains("Not authorized", StringComparison.OrdinalIgnoreCase) == true))
        {
            return "KWin ScreenShot2 permission denied. Install a desktop entry for CrossMacro that includes X-KDE-DBUS-Restricted-Interfaces=org.kde.KWin.ScreenShot2.";
        }

        return ex.Message;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException ex)
        {
            GC.KeepAlive(ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            GC.KeepAlive(ex);
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path);
            }
        }
        catch (IOException ex)
        {
            GC.KeepAlive(ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            GC.KeepAlive(ex);
        }
    }

    private readonly record struct KWinRawCapture(IReadOnlyDictionary<string, VariantValue> Results, byte[] Pixels);
}
