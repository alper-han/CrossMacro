using CrossMacro.Platform.Abstractions;
using Microsoft.Win32.SafeHandles;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public interface IPortalScreenCastCapture : IPortalScreenCastSupportProbe, IDisposable
{
    Task<PortalScreenCastCaptureResult> CaptureAsync(ScreenReadOptions options);

    Task<PortalScreenCastCaptureResult> CaptureSupportedAsync(ScreenReadOptions options);

    Task<PortalScreenCastCaptureResult> CaptureSupportedAsync(ScreenRect? region, ScreenReadOptions options);
}

public interface IPortalScreenCastSupportProbe
{
    PortalScreenCastSupportResult ProbeSupport();
}

public interface IPortalScreenCastSessionFactory
{
    Task<PortalScreenCastSessionResult> StartSessionAsync(ScreenReadOptions options);

    Task<PortalScreenCastSessionResult> StartSessionAsync(ScreenRect? requestedRegion, ScreenReadOptions options);
}

public interface IPortalScreenCastRestoreTokenStore
{
    string? LoadRestoreToken();

    Task SaveRestoreTokenAsync(string restoreToken);

    Task ClearRestoreTokenAsync();
}

public interface IPortalPipeWireFrameCaptureFactory
{
    IPortalPipeWireFrameCapture Create(SafeFileHandle pipeWireRemote, uint nodeId, int width, int height);
}

public interface IPortalPipeWireFrameCapture : IDisposable
{
    Task<PortalPipeWireFrameResult> CaptureFrameAsync(ScreenReadOptions options);
}

public sealed class PortalScreenCastSession : IDisposable
{
    private readonly IDisposable? _owner;
    private bool _disposed;

    public PortalScreenCastSession(
        string sessionHandle,
        IReadOnlyList<PortalStream> streams,
        SafeFileHandle pipeWireRemote,
        IDisposable? owner = null,
        string? restoreToken = null)
    {
        if (string.IsNullOrWhiteSpace(sessionHandle))
        {
            throw new ArgumentException("Portal sessions require a handle.", nameof(sessionHandle));
        }

        if (streams.Count == 0)
        {
            throw new ArgumentException("Portal sessions require at least one stream.", nameof(streams));
        }

        SessionHandle = sessionHandle;
        Streams = streams;
        PipeWireRemote = pipeWireRemote ?? throw new ArgumentNullException(nameof(pipeWireRemote));
        RestoreToken = string.IsNullOrWhiteSpace(restoreToken) ? null : restoreToken;
        _owner = owner;
    }

    public string SessionHandle { get; }

    public IReadOnlyList<PortalStream> Streams { get; }

    public SafeFileHandle PipeWireRemote { get; }

    public string? RestoreToken { get; }

    public PortalStream PrimaryStream => Streams[0];

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        PipeWireRemote.Dispose();
        _owner?.Dispose();
    }
}

public readonly record struct PortalStream(uint NodeId, IReadOnlyDictionary<string, object> Properties);

public sealed class PortalPipeWireFrame : IDisposable
{
    private readonly IDisposable? _owner;
    private bool _disposed;

    public PortalPipeWireFrame(ScreenRect logicalBounds, int stride, ScreenPixelFormat pixelFormat, ReadOnlyMemory<byte> pixels, IDisposable? owner = null)
    {
        var bytesPerPixel = ScreenFrame.GetBytesPerPixel(pixelFormat);
        var minimumStride = checked(logicalBounds.Width * bytesPerPixel);
        if (stride < minimumStride)
        {
            throw new ArgumentOutOfRangeException(nameof(stride), stride, "Portal PipeWire frame stride is smaller than its logical row width.");
        }

        var minimumLength = checked(stride * logicalBounds.Height);
        if (pixels.Length < minimumLength)
        {
            throw new ArgumentException("Portal PipeWire frame pixel memory is smaller than the declared frame dimensions.", nameof(pixels));
        }

        LogicalBounds = logicalBounds;
        Stride = stride;
        PixelFormat = pixelFormat;
        Pixels = pixels;
        _owner = owner;
    }

    public ScreenRect LogicalBounds { get; }

    public int Stride { get; }

    public ScreenPixelFormat PixelFormat { get; }

    public ReadOnlyMemory<byte> Pixels { get; }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _owner?.Dispose();
    }
}

public readonly record struct PortalScreenCastSupportResult
{
    private PortalScreenCastSupportResult(bool isSupported, ScreenReadErrorKind? errorKind, string? errorMessage)
    {
        if (!isSupported && string.IsNullOrWhiteSpace(errorMessage))
        {
            throw new ArgumentException("Unavailable portal probes require a message.", nameof(errorMessage));
        }

        IsSupported = isSupported;
        ErrorKind = errorKind;
        ErrorMessage = errorMessage;
    }

    public bool IsSupported { get; }

    public ScreenReadErrorKind? ErrorKind { get; }

    public string? ErrorMessage { get; }

    public static PortalScreenCastSupportResult Supported() => new(true, null, null);
    public static PortalScreenCastSupportResult Unsupported(string errorMessage) => new(false, ScreenReadErrorKind.BackendUnavailable, errorMessage);
    public static PortalScreenCastSupportResult Failure(ScreenReadErrorKind errorKind, string errorMessage) => new(false, errorKind, errorMessage);
}

public readonly record struct PortalScreenCastSessionResult
{
    private PortalScreenCastSessionResult(PortalScreenCastSession? session, ScreenReadErrorKind? errorKind, string? errorMessage)
    {
        if (session is null && string.IsNullOrWhiteSpace(errorMessage))
        {
            throw new ArgumentException("Failed portal sessions require a message.", nameof(errorMessage));
        }

        Session = session;
        ErrorKind = errorKind;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess => ErrorKind is null;
    public PortalScreenCastSession? Session { get; }
    public ScreenReadErrorKind? ErrorKind { get; }
    public string? ErrorMessage { get; }
    public static PortalScreenCastSessionResult Success(PortalScreenCastSession session) => new(session ?? throw new ArgumentNullException(nameof(session)), null, null);
    public static PortalScreenCastSessionResult Failure(ScreenReadErrorKind errorKind, string errorMessage) => new(null, errorKind, errorMessage);
}

public readonly record struct PortalPipeWireFrameResult
{
    private PortalPipeWireFrameResult(PortalPipeWireFrame? frame, ScreenReadErrorKind? errorKind, string? errorMessage)
    {
        if (frame is null && string.IsNullOrWhiteSpace(errorMessage))
        {
            throw new ArgumentException("Failed portal PipeWire captures require a message.", nameof(errorMessage));
        }

        Frame = frame;
        ErrorKind = errorKind;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess => ErrorKind is null;
    public PortalPipeWireFrame? Frame { get; }
    public ScreenReadErrorKind? ErrorKind { get; }
    public string? ErrorMessage { get; }
    public static PortalPipeWireFrameResult Success(PortalPipeWireFrame frame) => new(frame ?? throw new ArgumentNullException(nameof(frame)), null, null);
    public static PortalPipeWireFrameResult Failure(ScreenReadErrorKind errorKind, string errorMessage) => new(null, errorKind, errorMessage);
}

public readonly record struct PortalScreenCastCaptureResult
{
    private PortalScreenCastCaptureResult(PortalPipeWireFrame? frame, ScreenReadErrorKind? errorKind, string? errorMessage)
    {
        if (frame is null && string.IsNullOrWhiteSpace(errorMessage))
        {
            throw new ArgumentException("Failed portal captures require a message.", nameof(errorMessage));
        }

        Frame = frame;
        ErrorKind = errorKind;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess => ErrorKind is null;
    public PortalPipeWireFrame? Frame { get; }
    public ScreenReadErrorKind? ErrorKind { get; }
    public string? ErrorMessage { get; }
    public static PortalScreenCastCaptureResult Success(PortalPipeWireFrame frame) => new(frame ?? throw new ArgumentNullException(nameof(frame)), null, null);
    public static PortalScreenCastCaptureResult Failure(ScreenReadErrorKind errorKind, string errorMessage) => new(null, errorKind, errorMessage);
}
