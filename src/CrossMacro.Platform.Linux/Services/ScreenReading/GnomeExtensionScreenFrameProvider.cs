using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.Linux.DisplayServer.Wayland;
using System;
using System.Threading.Tasks;

namespace CrossMacro.Platform.Linux.Services.ScreenReading
{
    public readonly record struct GnomeExtensionSupportResult
    {
        private GnomeExtensionSupportResult(bool isSupported, ScreenReadErrorKind? errorKind, string? errorMessage)
        {
            IsSupported = isSupported;
            ErrorKind = errorKind;
            ErrorMessage = errorMessage;
        }

        public bool IsSupported { get; }
        public ScreenReadErrorKind? ErrorKind { get; }
        public string? ErrorMessage { get; }

        public static GnomeExtensionSupportResult Supported() => new(true, null, null);
        public static GnomeExtensionSupportResult Failure(ScreenReadErrorKind errorKind, string errorMessage) => new(false, errorKind, errorMessage);
    }

    public sealed class GnomeExtensionScreenFrameProvider : IScreenFrameProvider
    {
        private readonly GnomePositionProvider _positionProvider;
        private readonly GnomeExtensionSupportResult _support;
        private bool _disposed;

        public GnomeExtensionScreenFrameProvider(GnomePositionProvider positionProvider)
            : this(positionProvider, ProbeSupport(positionProvider))
        {
        }

        public GnomeExtensionScreenFrameProvider(GnomePositionProvider positionProvider, GnomeExtensionSupportResult support)
        {
            _positionProvider = positionProvider ?? throw new ArgumentNullException(nameof(positionProvider));
            _support = support;
        }

        public string ProviderName => "GNOME Shell Extension (RAM)";

        public bool IsSupported => _support.IsSupported;

        public async Task<ScreenReadResult<ScreenFrame>> CaptureFrameAsync(ScreenRect? region, ScreenReadOptions options)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (!_support.IsSupported)
            {
                return ScreenReadResult<ScreenFrame>.Failure(
                    _support.ErrorKind ?? ScreenReadErrorKind.BackendUnavailable,
                    _support.ErrorMessage ?? "GNOME Shell extension screen reading is unavailable.");
            }

            if (options.CancellationToken.IsCancellationRequested)
            {
                return LinuxScreenFrameProviderResults.CanceledBeforeStart("GNOME Shell extension screen capture was canceled before it started.");
            }

            var resolution = await _positionProvider.GetScreenResolutionAsync().ConfigureAwait(false);
            if (resolution is null)
            {
                return ScreenReadResult<ScreenFrame>.Failure(
                    ScreenReadErrorKind.CaptureFailed,
                    "Failed to retrieve screen resolution from GNOME extension.");
            }

            var bounds = region ?? new ScreenRect(0, 0, resolution.Value.Width, resolution.Value.Height);

            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return ScreenReadResult<ScreenFrame>.Failure(
                    ScreenReadErrorKind.OutOfBounds,
                    $"Invalid GNOME Shell extension capture region {bounds}.");
            }

            try
            {
                var captureResult = await _positionProvider.CaptureAreaAsync(bounds).ConfigureAwait(false);
                if (captureResult is null)
                {
                    return ScreenReadResult<ScreenFrame>.Failure(
                        ScreenReadErrorKind.CaptureFailed,
                        "GNOME Shell extension capture returned no data.");
                }

                var frame = new GnomeExtensionScreenFrame(bounds, captureResult.Value.Stride, captureResult.Value.Format, captureResult.Value.Pixels);
                
                return LinuxScreenFrameProviderResults.CreateSharedFrame(
                    frame.LogicalBounds,
                    frame.Stride,
                    frame.PixelFormat,
                    frame.Pixels,
                    frame);
            }
            catch (Exception ex) when (LinuxScreenFrameProviderResults.IsKnownCaptureException(ex))
            {
                return LinuxScreenFrameProviderResults.FromKnownCaptureException(ex, "GNOME Shell extension capture was canceled.");
            }
            catch (Exception ex)
            {
                return ScreenReadResult<ScreenFrame>.Failure(
                    ScreenReadErrorKind.CaptureFailed,
                    ex.Message);
            }
        }

        private static GnomeExtensionSupportResult ProbeSupport(GnomePositionProvider provider)
        {
            if (!provider.IsSupported)
            {
                return GnomeExtensionSupportResult.Failure(ScreenReadErrorKind.BackendUnavailable, "GNOME Shell is not active.");
            }

            var status = provider.CurrentExtensionStatus;
            if (status == null || status.Code != CrossMacro.Core.Services.ExtensionStatusCode.Enabled)
            {
                return GnomeExtensionSupportResult.Failure(
                    ScreenReadErrorKind.BackendUnavailable,
                    $"GNOME Shell extension backend is not active (Status: {status?.Code.ToString() ?? "Unknown"}).");
            }

            return GnomeExtensionSupportResult.Supported();
        }

        public void Dispose()
        {
            _disposed = true;
        }
    }

    internal sealed class GnomeExtensionScreenFrame : IDisposable
    {
        public GnomeExtensionScreenFrame(ScreenRect logicalBounds, int stride, ScreenPixelFormat pixelFormat, byte[] pixels)
        {
            LogicalBounds = logicalBounds;
            Stride = stride;
            PixelFormat = pixelFormat;
            Pixels = pixels;
        }

        public ScreenRect LogicalBounds { get; }
        public int Stride { get; }
        public ScreenPixelFormat PixelFormat { get; }
        public byte[] Pixels { get; }

        public void Dispose()
        {
        }
    }
}
