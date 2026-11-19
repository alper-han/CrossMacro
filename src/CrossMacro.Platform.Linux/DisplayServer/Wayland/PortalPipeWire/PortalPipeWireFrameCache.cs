namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.PortalPipeWire;

/// <summary>
/// Holds the most recent complete frame so requests that arrive between compositor frames are answered
/// from one consistent image instead of waiting for damage.
/// </summary>
internal sealed class PortalPipeWireFrameCache
{
    private readonly int _width;
    private readonly int _height;
    private readonly int _stride;
    private readonly object _gate = new();
    private byte[]? _pixels;
    private bool _hasFrame;
    private long _generation = -1;

    public PortalPipeWireFrameCache(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        _width = width;
        _height = height;
        _stride = checked(width * PipeWireConstants.Xrgb8888BytesPerPixel);
    }

    public bool TryCreateFrame(ScreenRect region, out WaylandScreenFrame? frame)
    {
        ValidateRegion(region);
        lock (_gate)
        {
            if (!_hasFrame || _pixels is null)
            {
                frame = null;
                return false;
            }

            var targetStride = checked(region.Width * PipeWireConstants.Xrgb8888BytesPerPixel);
            var pixels = new byte[checked(targetStride * region.Height)];
            for (var row = 0; row < region.Height; row++)
            {
                var sourceOffset = checked(((region.Y + row) * _stride) + (region.X * PipeWireConstants.Xrgb8888BytesPerPixel));
                var targetOffset = row * targetStride;
                _pixels.AsSpan(sourceOffset, targetStride).CopyTo(pixels.AsSpan(targetOffset, targetStride));
            }

            frame = new WaylandScreenFrame(
                new ScreenRect(0, 0, region.Width, region.Height),
                targetStride,
                ScreenPixelFormat.Xrgb8888,
                pixels);
            return true;
        }
    }

    /// <summary>
    /// Starts replacing the cached image. The cache holds no frame until <see cref="FullUpdate.Commit"/> runs, and
    /// updates that are not newer than the cached generation are rejected.
    /// </summary>
    public FullUpdate BeginFullUpdate(long generation)
    {
        Monitor.Enter(_gate);
        if (generation <= _generation)
        {
            Monitor.Exit(_gate);
            return default;
        }

        _pixels ??= new byte[checked(_stride * _height)];
        _hasFrame = false;
        return new FullUpdate(this, generation);
    }

    public void Clear()
    {
        lock (_gate)
        {
            _pixels = null;
            _hasFrame = false;
            _generation = -1;
        }
    }

    private void CompleteFullUpdate(long generation)
    {
        _hasFrame = true;
        _generation = generation;
    }

    private void ValidateRegion(ScreenRect region)
    {
        if (region.X < 0 || region.Y < 0 || region.Right > _width || region.Bottom > _height)
        {
            throw new ArgumentOutOfRangeException(
                nameof(region),
                region,
                $"PipeWire cache region must be inside 0,0 {_width.ToString(CultureInfo.InvariantCulture)}x{_height.ToString(CultureInfo.InvariantCulture)}.");
        }
    }

    public ref struct FullUpdate
    {
        private readonly PortalPipeWireFrameCache? _owner;
        private readonly long _generation;
        private bool _committed;

        internal FullUpdate(PortalPipeWireFrameCache owner, long generation)
        {
            _owner = owner;
            _generation = generation;
            _committed = false;
        }

        public readonly bool IsAccepted => _owner is not null;

        public readonly Span<byte> Pixels => _owner is null ? Span<byte>.Empty : _owner._pixels!;

        public void Commit()
        {
            if (_owner is null || _committed)
            {
                return;
            }

            _owner.CompleteFullUpdate(_generation);
            _committed = true;
        }

        public readonly void Dispose()
        {
            if (_owner is not null)
            {
                Monitor.Exit(_owner._gate);
            }
        }
    }
}
