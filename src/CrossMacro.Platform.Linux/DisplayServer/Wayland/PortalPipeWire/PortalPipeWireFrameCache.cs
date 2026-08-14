namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.PortalPipeWire;

internal sealed class PortalPipeWireFrameCache
{
    private readonly int _width;
    private readonly int _height;
    private readonly int _stride;
    private readonly object _gate = new();
    private byte[]? _pixels;
    private byte[] _coverage = [];
    private int _coveredPixels;
    private bool _fullyCovered;
    private long _generation = -1;

    public PortalPipeWireFrameCache(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        _width = width;
        _height = height;
        _stride = checked(width * PipeWireConstants.Xrgb8888BytesPerPixel);
    }

    public bool TryCreateFrame(ScreenRect region, out PortalPipeWireFrame? frame)
    {
        ValidateRegion(region);
        lock (_gate)
        {
            if (_pixels is null || !IsCovered(region))
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

            frame = new PortalPipeWireFrame(
                new ScreenRect(0, 0, region.Width, region.Height),
                targetStride,
                ScreenPixelFormat.Xrgb8888,
                pixels);
            return true;
        }
    }

    public void Update(ScreenRect region, ReadOnlySpan<byte> pixels, int sourceStride, long generation)
    {
        ValidateRegion(region);
        var minimumStride = checked(region.Width * PipeWireConstants.Xrgb8888BytesPerPixel);
        if (sourceStride < minimumStride || pixels.Length < checked(sourceStride * region.Height))
        {
            throw new ArgumentException("PipeWire cache update does not contain the declared region.", nameof(pixels));
        }

        lock (_gate)
        {
            if (generation <= _generation)
            {
                return;
            }

            EnsureStorage();
            for (var row = 0; row < region.Height; row++)
            {
                var sourceOffset = row * sourceStride;
                var targetOffset = checked(((region.Y + row) * _stride) + (region.X * PipeWireConstants.Xrgb8888BytesPerPixel));
                pixels.Slice(sourceOffset, minimumStride).CopyTo(_pixels.AsSpan(targetOffset, minimumStride));

                if (!_fullyCovered)
                {
                    var coverageOffset = checked(((region.Y + row) * _width) + region.X);
                    for (var column = 0; column < region.Width; column++)
                    {
                        var index = coverageOffset + column;
                        if (_coverage[index] is 0)
                        {
                            _coverage[index] = 1;
                            _coveredPixels++;
                        }
                    }
                }
            }

            _fullyCovered = _coveredPixels == checked(_width * _height);
            _generation = generation;
        }
    }

    public FullUpdate BeginFullUpdate(long generation)
    {
        Monitor.Enter(_gate);
        if (generation <= _generation)
        {
            Monitor.Exit(_gate);
            return default;
        }

        EnsureStorage();
        return new FullUpdate(this, generation);
    }

    public void Clear()
    {
        lock (_gate)
        {
            _pixels = null;
            _coverage = [];
            _coveredPixels = 0;
            _fullyCovered = false;
            _generation = -1;
        }
    }

    private void CompleteFullUpdate(long generation)
    {
        _fullyCovered = true;
        _coveredPixels = checked(_width * _height);
        _generation = generation;
    }

    private void EnsureStorage()
    {
        if (_pixels is not null)
        {
            return;
        }

        _pixels = new byte[checked(_stride * _height)];
        _coverage = new byte[checked(_width * _height)];
    }

    private bool IsCovered(ScreenRect region)
    {
        if (_fullyCovered)
        {
            return true;
        }

        if (_coveredPixels is 0)
        {
            return false;
        }

        for (var row = 0; row < region.Height; row++)
        {
            var offset = checked(((region.Y + row) * _width) + region.X);
            if (_coverage.AsSpan(offset, region.Width).IndexOf((byte)0) >= 0)
            {
                return false;
            }
        }

        return true;
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
