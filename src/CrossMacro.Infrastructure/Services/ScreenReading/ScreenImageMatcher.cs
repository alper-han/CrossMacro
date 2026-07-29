
namespace CrossMacro.Infrastructure.Services.ScreenReading;

public sealed class ScreenImageMatcher : IDisposable
{
    private const int ColorChannelCount = 3;
    private const int MaxChannelDifference = byte.MaxValue;
    private const int CancellationCheckBlockBytes = 4096;
    private const long ParallelPixelThreshold = 256_000;
    private const int MinimumParallelRowWidth = 256;
    private const int MinimumParallelRowCount = 4;
    internal const long MaxMatcherWork = 100_000_000;
    private const int MatcherRowBandHeight = 32;
    internal const long MaxTemplateCacheBytes = 64L * 1024 * 1024;
    private static readonly double[] PrimaryScales = [1.0, 0.9, 1.1, 0.8, 1.2, 1.25, 0.75, 1.5];
    private static readonly double[] SecondaryScales = [0.95, 1.05, 0.85, 1.15, 1.3, 0.7, 1.35, 1.4, 1.45];

    private readonly Lock _lifetimeLock = new();
    private readonly Lock _templateCacheLock = new();
    private readonly ManualResetEventSlim _searchesCompleted = new(initialState: false);
    private readonly ManualResetEventSlim _disposeCompleted = new(initialState: false);
    private readonly Dictionary<TemplateCacheKey, LinkedListNode<TemplateCacheEntry>> _templateCache = new(TemplateCacheKeyComparer.Instance);
    private readonly LinkedList<TemplateCacheEntry> _templateCacheLru = new();
    private readonly long _maxTemplateCacheBytes;
    private long _templateCacheBytes;
    private int _templateNormalizationCount;
    private long _activeSearchCount;
    private bool _disposeRequested;
    private bool _disposed;

    public ScreenImageMatcher()
        : this(MaxTemplateCacheBytes)
    {
    }

    internal ScreenImageMatcher(long maxTemplateCacheBytes)
    {
        if (maxTemplateCacheBytes is < 1 or > MaxTemplateCacheBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(maxTemplateCacheBytes), maxTemplateCacheBytes, $"Template cache size must be between 1 and {MaxTemplateCacheBytes} bytes.");
        }

        _maxTemplateCacheBytes = maxTemplateCacheBytes;
    }

    internal int TemplateNormalizationCount => Volatile.Read(ref _templateNormalizationCount);

    public ScreenImageMatch? FindMatch(
        ScreenFrame frame,
        ScreenFrame template,
        ScreenImageMatchOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        using var searchLease = EnterSearchLease();
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(template);
        cancellationToken.ThrowIfCancellationRequested();

        options ??= ScreenImageMatchOptions.Default;
        ValidateOptions(options);
        EnsureReadable(frame);
        EnsureReadable(template);
        cancellationToken.ThrowIfCancellationRequested();

        var region = options.SearchRegion ?? frame.LogicalBounds;
        if (!frame.LogicalBounds.Contains(region))
        {
            throw new ArgumentOutOfRangeException(nameof(options), region, "The image search region is outside the frame bounds.");
        }

        if (template.Width > region.Width || template.Height > region.Height)
        {
            return null;
        }

        if (options.ScaleAware)
        {
            return FindScaleAwareMatchWithPooledFrame(frame, template, region, options, cancellationToken);
        }

        var sampleWidth = GetSampleCount(template.Width, options.DownsampleFactor);
        var sampleHeight = GetSampleCount(template.Height, options.DownsampleFactor);
        var samplePixelCount = checked((long)sampleWidth * sampleHeight);
        if (samplePixelCount > MaxMatcherWork / ColorChannelCount)
        {
            var requestedWork = samplePixelCount > long.MaxValue / ColorChannelCount
                ? long.MaxValue
                : samplePixelCount * ColorChannelCount;
            throw new ScreenImageMatcherResourceLimitException(
                requestedWork,
                MaxMatcherWork,
                $"A single image matcher candidate requires more than {MaxMatcherWork.ToString("N0", CultureInfo.InvariantCulture)} channel comparisons, exceeding the internal limit.");
        }

        var requestedAnchorCount = Math.Min((long)options.AnchorPointCount, samplePixelCount);
        if (samplePixelCount + requestedAnchorCount > MaxMatcherWork / ColorChannelCount)
        {
            var requestedWork = checked((samplePixelCount + requestedAnchorCount) * ColorChannelCount);
            throw new ScreenImageMatcherResourceLimitException(
                requestedWork,
                MaxMatcherWork,
                $"A single image matcher candidate, including its requested prefilter, requires {requestedWork.ToString("N0", CultureInfo.InvariantCulture)} channel comparisons, exceeding the internal limit of {MaxMatcherWork.ToString("N0", CultureInfo.InvariantCulture)}.");
        }

        var anchors = BuildAnchorPoints(sampleWidth, sampleHeight, options.DownsampleFactor, options.AnchorPointCount);
        var singleCandidateWork = checked((samplePixelCount + anchors.LongLength) * ColorChannelCount);
        if (singleCandidateWork > MaxMatcherWork)
        {
            throw new ScreenImageMatcherResourceLimitException(
                singleCandidateWork,
                MaxMatcherWork,
                $"A single image matcher candidate, including its prefilter, requires {singleCandidateWork.ToString("N0", CultureInfo.InvariantCulture)} channel comparisons, exceeding the internal limit of {MaxMatcherWork.ToString("N0", CultureInfo.InvariantCulture)}.");
        }

        var framePixels = NormalizePooledFrame(frame, cancellationToken);
        try
        {
            var maximumSad = samplePixelCount * ColorChannelCount * (double)MaxChannelDifference;
            var allowedSad = CalculateAllowedSad(maximumSad, options.MinimumSimilarity);
            var templatePixels = GetNormalizedTemplate(template, options.DownsampleFactor, cancellationToken);

            var candidateWidth = checked((long)region.Width - template.Width + 1);
            var candidateHeight = checked((long)region.Height - template.Height + 1);
            var selectedCandidate = MatchCandidate.None;
            for (long bandYOffset = 0; bandYOffset < candidateHeight; bandYOffset = checked(bandYOffset + MatcherRowBandHeight))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var bandHeight = Math.Min(MatcherRowBandHeight, candidateHeight - bandYOffset);
                var startY = checked((long)region.Y + bandYOffset);
                var endY = checked(startY + bandHeight);
                var bandCandidate = FindBestCandidate(
                    framePixels,
                    frame,
                    templatePixels,
                    frame.LogicalBounds,
                    region.X,
                    checked((long)region.X + candidateWidth),
                    startY,
                    endY,
                    anchors,
                    allowedSad,
                    options.DownsampleFactor,
                    options.SelectionMode,
                    cancellationToken);

                if (options.SelectionMode is ScreenImageMatchSelectionMode.FirstThresholdMatch)
                {
                    if (bandCandidate.HasValue)
                    {
                        selectedCandidate = bandCandidate;
                        break;
                    }
                }
                else
                {
                    selectedCandidate = BetterOf(selectedCandidate, bandCandidate);
                }
            }

            if (!selectedCandidate.HasValue)
            {
                return null;
            }

            return new ScreenImageMatch(new ScreenPoint(selectedCandidate.X, selectedCandidate.Y), CalculateScore(selectedCandidate.Sad, maximumSad));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(framePixels.Pixels);
        }
    }

    public void Dispose()
    {
        bool alreadyRequested;
        bool hasActiveSearches;

        lock (_lifetimeLock)
        {
            if (_disposed)
            {
                return;
            }

            alreadyRequested = _disposeRequested;

            if (!alreadyRequested)
            {
                _disposeRequested = true;
                hasActiveSearches = _activeSearchCount > 0;
            }
            else
            {
                hasActiveSearches = true; // trigger the wait-on-_disposeCompleted path below
            }
        }

        if (alreadyRequested)
        {
            // Another caller is already disposing: wait for it to complete.
            _disposeCompleted.Wait();
            return;
        }

        if (hasActiveSearches)
        {
            // Wait for in-flight searches to drain. ExitSearchLease will set
            // _searchesCompleted when the last lease is released.
            _searchesCompleted.Wait();
        }

        lock (_templateCacheLock)
        {
            _templateCache.Clear();
            _templateCacheLru.Clear();
            _templateCacheBytes = 0;
        }

        lock (_lifetimeLock)
        {
            _disposed = true;
        }

        // Wake any waiting second Dispose callers and dispose the events.
        _searchesCompleted.Set();
        _disposeCompleted.Set();
        _searchesCompleted.Dispose();
        _disposeCompleted.Dispose();
    }

    private MatcherSearchLease EnterSearchLease()
    {
        lock (_lifetimeLock)
        {
            ObjectDisposedException.ThrowIf(_disposeRequested, this);
            _activeSearchCount++;
            return new MatcherSearchLease(this);
        }
    }

    private void ExitSearchLease()
    {
        bool signalDispose;
        lock (_lifetimeLock)
        {
            _activeSearchCount--;
            signalDispose = _disposeRequested && _activeSearchCount == 0;
        }

        if (signalDispose)
        {
            _searchesCompleted.Set();
        }
    }

    private static MatchCandidate FindBestCandidate(
        RgbImage frame,
        ScreenFrame validityFrame,
        RgbImage template,
        ScreenRect frameBounds,
        long startX,
        long endX,
        long startY,
        long endY,
        AnchorPoint[] anchors,
        long allowedSad,
        int downsampleFactor,
        ScreenImageMatchSelectionMode selectionMode,
        CancellationToken cancellationToken)
    {
        if (downsampleFactor is 1 && template.Width >= 16 && template.Height >= 16)
        {
            var result = FindBestCandidateCoarseToFine(
                frame,
                validityFrame,
                template,
                frameBounds,
                startX,
                endX,
                startY,
                endY,
                anchors,
                allowedSad,
                selectionMode,
                cancellationToken);

            double maxSad = template.Width * template.Height * ColorChannelCount * 255.0;
            double sim = 1.0 - (allowedSad / maxSad);

            if (result.HasValue || sim >= 0.7)
            {
                return result;
            }
        }

        return FindBestCandidateStandard(
            frame,
            validityFrame,
            template,
            frameBounds,
            startX,
            endX,
            startY,
            endY,
            anchors,
            allowedSad,
            downsampleFactor,
            selectionMode,
            cancellationToken);
    }

    private static MatchCandidate FindBestCandidateStandard(
        RgbImage frame,
        ScreenFrame validityFrame,
        RgbImage template,
        ScreenRect frameBounds,
        long startX,
        long endX,
        long startY,
        long endY,
        AnchorPoint[] anchors,
        long allowedSad,
        int downsampleFactor,
        ScreenImageMatchSelectionMode selectionMode,
        CancellationToken cancellationToken)
    {
        var bestCandidate = MatchCandidate.None;
        var bestCandidateLock = new Lock();
        var frameFullyValid = validityFrame.IsFullyValid;
        var parallelOptions = new ParallelOptions { CancellationToken = cancellationToken };
        _ = Parallel.For(startY, endY, parallelOptions, candidateYValue =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var candidateY = checked((int)candidateYValue);
            var rowBest = FindBestCandidateInRow(
                frame,
                validityFrame,
                frameFullyValid,
                template,
                frameBounds,
                startX,
                endX,
                candidateY,
                anchors,
                allowedSad,
                downsampleFactor,
                selectionMode,
                cancellationToken);

            if (rowBest.HasValue)
            {
                lock (bestCandidateLock)
                {
                    bestCandidate = selectionMode is ScreenImageMatchSelectionMode.FirstThresholdMatch
                        ? EarlierOf(bestCandidate, rowBest)
                        : BetterOf(bestCandidate, rowBest);
                }
            }
        });

        return bestCandidate;
    }

    private static MatchCandidate FindBestCandidateCoarseToFine(
        RgbImage frame,
        ScreenFrame validityFrame,
        RgbImage template,
        ScreenRect frameBounds,
        long startX,
        long endX,
        long startY,
        long endY,
        AnchorPoint[] anchors,
        long allowedSad,
        ScreenImageMatchSelectionMode selectionMode,
        CancellationToken cancellationToken)
    {
        var templateDown = DownsampleBy2(template);

        int minX = (int)startX;
        int maxX = (int)(endX + template.Width - 1);
        int minY = (int)startY;
        int maxY = (int)(endY + template.Height - 1);
        int regionW = maxX - minX + 1;
        int regionH = maxY - minY + 1;

        if (regionW < template.Width || regionH < template.Height)
        {
            return MatchCandidate.None;
        }

        var frameLocalX = checked(minX - frameBounds.X);
        var frameLocalY = checked(minY - frameBounds.Y);
        var frameDown = CropAndDownsampleBy2(frame, frameLocalX, frameLocalY, regionW, regionH);

        const int startXDown = 0;
        int endXDown = frameDown.Width - templateDown.Width + 1;
        int endYDown = frameDown.Height - templateDown.Height + 1;

        if (endXDown <= 0 || endYDown <= 0)
        {
            return MatchCandidate.None;
        }

        var anchorsDown = BuildAnchorPoints(
            GetSampleCount(templateDown.Width, 1),
            GetSampleCount(templateDown.Height, 1),
            1,
            anchors.Length);

        double targetSimilarity = 1.0 - ((double)allowedSad / (template.Width * template.Height * ColorChannelCount * MaxChannelDifference));
        double coarseSimilarity = Math.Max(0.5, targetSimilarity - 0.15); // Lower similarity threshold by 15% for decimation and phase shift margin
        double maximumSadDown = (double)templateDown.Width * templateDown.Height * ColorChannelCount * MaxChannelDifference;
        long allowedSadDown = CalculateAllowedSad(maximumSadDown, coarseSimilarity);

        var bestCandidate = MatchCandidate.None;
        var bestCandidateLock = new Lock();
        var parallelOptions = new ParallelOptions { CancellationToken = cancellationToken };

        _ = Parallel.For(0, endYDown, parallelOptions, yDown =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rowBestDown = FindBestCandidateInRow(
                frameDown,
                validityFrame,
frameFullyValid: true,
                templateDown,
                new ScreenRect(0, 0, frameDown.Width, frameDown.Height),
                startXDown,
                endXDown,
                yDown,
                anchorsDown,
                allowedSadDown,
                1,
                ScreenImageMatchSelectionMode.BestMatch,
                cancellationToken);

            if (rowBestDown.HasValue)
            {
                lock (bestCandidateLock)
                {
                    bestCandidate = BetterOf(bestCandidate, rowBestDown);
                }
            }
        });

        if (!bestCandidate.HasValue)
        {
            return MatchCandidate.None;
        }

        int cx = minX + (bestCandidate.X * 2);
        int cy = minY + (bestCandidate.Y * 2);

        int refMinX = Math.Max((int)startX, cx - 2);
        int refMaxX = Math.Min((int)endX - 1, cx + 2);
        int refMinY = Math.Max((int)startY, cy - 2);
        int refMaxY = Math.Min((int)endY - 1, cy + 2);

        return FindBestCandidateStandard(
            frame,
            validityFrame,
            template,
            frameBounds,
            refMinX,
            refMaxX + 1,
            refMinY,
            refMaxY + 1,
            anchors,
            allowedSad,
            1,
            selectionMode,
            cancellationToken);
    }

    private static RgbImage DownsampleBy2(RgbImage source)
    {
        int w = source.Width / 2;
        int h = source.Height / 2;
        byte[] pixels = new byte[w * h * ColorChannelCount];

        void CopyRow(int y)
        {
            int sourceY = y * 2;
            int targetRowOffset = y * w * ColorChannelCount;
            int sourceRowOffset = sourceY * source.RowStride;
            for (int x = 0; x < w; x++)
            {
                int sourceX = x * 2;
                int sourceOffset = sourceRowOffset + (sourceX * ColorChannelCount);
                int targetOffset = targetRowOffset + (x * ColorChannelCount);
                pixels[targetOffset] = source.Pixels[sourceOffset];
                pixels[targetOffset + 1] = source.Pixels[sourceOffset + 1];
                pixels[targetOffset + 2] = source.Pixels[sourceOffset + 2];
            }
        }

        if (ShouldParallelizeRows(w, h))
        {
            _ = Parallel.For(0, h, CopyRow);
        }
        else
        {
            for (int y = 0; y < h; y++)
            {
                CopyRow(y);
            }
        }

        return new RgbImage(w, h, pixels, w * ColorChannelCount);
    }

    private static RgbImage CropAndDownsampleBy2(RgbImage source, int startX, int startY, int width, int height)
    {
        int w = width / 2;
        int h = height / 2;
        byte[] pixels = new byte[w * h * ColorChannelCount];

        void CopyRow(int y)
        {
            int sourceY = startY + (y * 2);
            int targetRowOffset = y * w * ColorChannelCount;
            int sourceRowOffset = sourceY * source.RowStride;
            for (int x = 0; x < w; x++)
            {
                int sourceX = startX + (x * 2);
                int sourceOffset = sourceRowOffset + (sourceX * ColorChannelCount);
                int targetOffset = targetRowOffset + (x * ColorChannelCount);
                pixels[targetOffset] = source.Pixels[sourceOffset];
                pixels[targetOffset + 1] = source.Pixels[sourceOffset + 1];
                pixels[targetOffset + 2] = source.Pixels[sourceOffset + 2];
            }
        }

        if (ShouldParallelizeRows(w, h))
        {
            _ = Parallel.For(0, h, CopyRow);
        }
        else
        {
            for (int y = 0; y < h; y++)
            {
                CopyRow(y);
            }
        }

        return new RgbImage(w, h, pixels, w * ColorChannelCount);
    }

    private static MatchCandidate FindBestCandidateInRow(
        RgbImage frame,
        ScreenFrame validityFrame,
        bool frameFullyValid,
        RgbImage template,
        ScreenRect frameBounds,
        long startX,
        long endX,
        int candidateY,
        AnchorPoint[] anchors,
        long allowedSad,
        int downsampleFactor,
        ScreenImageMatchSelectionMode selectionMode,
        CancellationToken cancellationToken)
    {
        var rowBest = MatchCandidate.None;
        var earlySuccess = new EarlySuccessSignal();
        for (var candidateXValue = startX; candidateXValue < endX; candidateXValue++)
        {
            if ((candidateXValue - startX) % 32 == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
            if (earlySuccess.IsRequested)
            {
                break;
            }

            var candidateX = checked((int)candidateXValue);
            if (!frameFullyValid && !IsRegionValid(validityFrame, candidateX, candidateY, template.Width, template.Height))
            {
                continue;
            }

            var candidateLimit = selectionMode is ScreenImageMatchSelectionMode.BestMatch && rowBest.HasValue
                ? Math.Min(allowedSad, rowBest.Sad)
                : allowedSad;
            if (!PassesAnchorPrefilter(frame, template, frameBounds, candidateX, candidateY, anchors, candidateLimit, cancellationToken))
            {
                continue;
            }

            var sad = TryComputeSad(frame, template, frameBounds, candidateX, candidateY, downsampleFactor, candidateLimit, cancellationToken);
            if (sad is null)
            {
                continue;
            }

            var candidate = new MatchCandidate(candidateX, candidateY, sad.Value);
            if (selectionMode is ScreenImageMatchSelectionMode.FirstThresholdMatch)
            {
                rowBest = candidate;
                earlySuccess.Request();
            }
            else
            {
                rowBest = BetterOf(rowBest, candidate);
            }
        }

        return rowBest;
    }

    private static void ValidateOptions(ScreenImageMatchOptions options)
    {
        if (!Enum.IsDefined(options.SelectionMode))
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.SelectionMode,
                "Image match selection mode is invalid.");
        }

        if (!double.IsFinite(options.MinimumSimilarity) || options.MinimumSimilarity is < 0.0 or > 1.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.MinimumSimilarity,
                "Minimum similarity must be between 0.0 and 1.0.");
        }

        if (options.DownsampleFactor < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.DownsampleFactor,
                "Downsample factor must be at least 1.");
        }

        if (options.AnchorPointCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.AnchorPointCount,
                "Anchor point count cannot be negative.");
        }
    }

    private static void EnsureReadable(ScreenFrame frame)
    {
        _ = frame.TryGetPixel(new ScreenPoint(frame.LogicalBounds.X, frame.LogicalBounds.Y), out _);
    }

    private static int GetSampleCount(int length, int downsampleFactor) => ((length - 1) / downsampleFactor) + 1;

    private RgbImage GetNormalizedTemplate(ScreenFrame template, int downsampleFactor, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = CreateTemplateCacheKey(template, downsampleFactor, cancellationToken);
        lock (_templateCacheLock)
        {
            if (_templateCache.TryGetValue(key, out var cached))
            {
                _templateCacheLru.Remove(cached);
                _templateCacheLru.AddFirst(cached);
                return cached.Value.Image;
            }
        }

        var normalized = NormalizeFrame(template, cancellationToken);
        _ = Interlocked.Increment(ref _templateNormalizationCount);
        var entryBytes = checked((long)key.Content.Length + normalized.Pixels.LongLength);
        if (entryBytes > _maxTemplateCacheBytes)
        {
            return normalized;
        }

        lock (_templateCacheLock)
        {
            if (_templateCache.TryGetValue(key, out var cached))
            {
                _templateCacheLru.Remove(cached);
                _templateCacheLru.AddFirst(cached);
                return cached.Value.Image;
            }

            while (_templateCacheBytes + entryBytes > _maxTemplateCacheBytes && _templateCacheLru.Last is { } oldest)
            {
                RemoveTemplateCacheEntry(oldest);
            }

            var entry = new TemplateCacheEntry(key, normalized, entryBytes);
            var node = _templateCacheLru.AddFirst(entry);
            _templateCache.Add(key, node);
            _templateCacheBytes += entryBytes;
            return normalized;
        }
    }

    private ScreenImageMatch? FindScaleAwareMatchWithPooledFrame(ScreenFrame frame, ScreenFrame template, ScreenRect region, ScreenImageMatchOptions options, CancellationToken cancellationToken)
    {
        var framePixels = NormalizePooledFrame(frame, cancellationToken);
        try
        {
            return FindScaleAwareMatch(frame, framePixels, template, region, options, cancellationToken);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(framePixels.Pixels);
        }
    }

    private ScreenImageMatch? FindScaleAwareMatch(ScreenFrame frame, RgbImage framePixels, ScreenFrame template, ScreenRect region, ScreenImageMatchOptions options, CancellationToken cancellationToken)
    {
        var baseTemplate = GetNormalizedTemplate(template, options.DownsampleFactor, cancellationToken);
        var selected = MatchCandidate.None;

        foreach (var scale in PrimaryScales)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var width = Math.Max(1, (int)Math.Round(template.Width * scale, MidpointRounding.AwayFromZero));
            var height = Math.Max(1, (int)Math.Round(template.Height * scale, MidpointRounding.AwayFromZero));
            if (width > region.Width || height > region.Height)
            {
                continue;
            }

            var scaled = scale is 1.0
                ? baseTemplate
                : GetScaledTemplate(template, baseTemplate, width, height, scale, options.DownsampleFactor, cancellationToken);
            var candidate = FindScaledCandidate(frame, framePixels, scaled, region, options, scale, cancellationToken);
            if (options.SelectionMode is ScreenImageMatchSelectionMode.FirstThresholdMatch)
            {
                if (!selected.HasValue || (candidate.HasValue && candidate.Y == selected.Y && candidate.X == selected.X
                    && Math.Abs(candidate.Scale - 1.0) < Math.Abs(selected.Scale - 1.0)))
                {
                    selected = candidate;
                }

                if (selected.HasValue)
                {
                    break;
                }
            }
            else
            {
                selected = BetterScaleCandidate(selected, candidate);
            }
        }

        const double scoreThreshold = 0.95;
        var selectedScore = selected.HasValue ? CalculateScore(selected.Sad, selected.MaximumSad) : 0.0;
        if (selected.HasValue && (options.SelectionMode is ScreenImageMatchSelectionMode.FirstThresholdMatch || selectedScore >= scoreThreshold))
        {
            return new ScreenImageMatch(new ScreenPoint(selected.X, selected.Y), selectedScore, selected.Width, selected.Height);
        }

        foreach (var scale in SecondaryScales)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var width = Math.Max(1, (int)Math.Round(template.Width * scale, MidpointRounding.AwayFromZero));
            var height = Math.Max(1, (int)Math.Round(template.Height * scale, MidpointRounding.AwayFromZero));
            if (width > region.Width || height > region.Height)
            {
                continue;
            }

            var scaled = scale is 1.0
                ? baseTemplate
                : GetScaledTemplate(template, baseTemplate, width, height, scale, options.DownsampleFactor, cancellationToken);
            var candidate = FindScaledCandidate(frame, framePixels, scaled, region, options, scale, cancellationToken);
            if (options.SelectionMode is ScreenImageMatchSelectionMode.FirstThresholdMatch)
            {
                if (!selected.HasValue || (candidate.HasValue && candidate.Y == selected.Y && candidate.X == selected.X
                    && Math.Abs(candidate.Scale - 1.0) < Math.Abs(selected.Scale - 1.0)))
                {
                    selected = candidate;
                }

                if (selected.HasValue)
                {
                    break;
                }
            }
            else
            {
                selected = BetterScaleCandidate(selected, candidate);
            }
        }

        return selected.HasValue
            ? new ScreenImageMatch(new ScreenPoint(selected.X, selected.Y), CalculateScore(selected.Sad, selected.MaximumSad), selected.Width, selected.Height)
            : null;
    }

    private static MatchCandidate FindScaledCandidate(ScreenFrame frame, RgbImage framePixels, RgbImage template, ScreenRect region, ScreenImageMatchOptions options, double scale, CancellationToken cancellationToken)
    {
        var sampleWidth = GetSampleCount(template.Width, options.DownsampleFactor);
        var sampleHeight = GetSampleCount(template.Height, options.DownsampleFactor);
        var samplePixelCount = checked((long)sampleWidth * sampleHeight);
        var maximumSad = samplePixelCount * ColorChannelCount * (double)MaxChannelDifference;
        var allowedSad = CalculateAllowedSad(maximumSad, options.MinimumSimilarity);
        var anchors = BuildAnchorPoints(sampleWidth, sampleHeight, options.DownsampleFactor, options.AnchorPointCount);
        var candidateWidth = checked((long)region.Width - template.Width + 1);
        var candidateHeight = checked((long)region.Height - template.Height + 1);

        var best = FindBestCandidate(
            framePixels,
            frame,
            template,
            frame.LogicalBounds,
            region.X,
            checked(region.X + candidateWidth),
            region.Y,
            checked(region.Y + candidateHeight),
            anchors,
            allowedSad,
            options.DownsampleFactor,
            options.SelectionMode,
            cancellationToken);

        if (best.HasValue)
        {
            return new MatchCandidate(best.X, best.Y, best.Sad, maximumSad, template.Width, template.Height, scale);
        }
        return MatchCandidate.None;
    }

    private RgbImage GetScaledTemplate(ScreenFrame template, RgbImage source, int width, int height, double scale, int downsampleFactor, CancellationToken cancellationToken)
    {
        var key = CreateTemplateCacheKey(template, downsampleFactor, cancellationToken, (int)Math.Round(scale * 1000, MidpointRounding.AwayFromZero)) with { Width = width, Height = height };
        lock (_templateCacheLock)
        {
            if (_templateCache.TryGetValue(key, out var cached))
            {
                _templateCacheLru.Remove(cached);
                _templateCacheLru.AddFirst(cached);
                return cached.Value.Image;
            }
        }

        var pixels = new byte[checked(width * height * ColorChannelCount)];
        for (var y = 0; y < height; y++)
        {
            var sourceY = Math.Min(source.Height - 1, (int)Math.Floor(y / scale));
            for (var x = 0; x < width; x++)
            {
                var sourceX = Math.Min(source.Width - 1, (int)Math.Floor(x / scale));
                var sourceOffset = checked(((sourceY * source.Width) + sourceX) * ColorChannelCount);
                var targetOffset = checked(((y * width) + x) * ColorChannelCount);
                source.Pixels.AsSpan(sourceOffset, ColorChannelCount).CopyTo(pixels.AsSpan(targetOffset, ColorChannelCount));
            }
        }

        var scaled = new RgbImage(width, height, pixels, checked(width * ColorChannelCount));
        var entryBytes = checked((long)key.Content.Length + pixels.LongLength);
        if (entryBytes <= _maxTemplateCacheBytes)
        {
            lock (_templateCacheLock)
            {
                while (_templateCacheBytes + entryBytes > _maxTemplateCacheBytes && _templateCacheLru.Last is { } oldest)
                {
                    RemoveTemplateCacheEntry(oldest);
                }
                var entry = new TemplateCacheEntry(key, scaled, entryBytes);
                var node = _templateCacheLru.AddFirst(entry);
                _templateCache.Add(key, node);
                _templateCacheBytes += entryBytes;
            }
        }
        return scaled;
    }

    private static MatchCandidate BetterScaleCandidate(MatchCandidate current, MatchCandidate candidate)
    {
        if (!candidate.HasValue || !current.HasValue)
        {
            return candidate.HasValue ? candidate : current;
        }
        var candidateDistance = Math.Abs(candidate.Scale - 1.0);
        var currentDistance = Math.Abs(current.Scale - 1.0);
        return candidate.Sad < current.Sad
            || (candidate.Sad == current.Sad && (candidate.Y < current.Y
                || (candidate.Y == current.Y && (candidate.X < current.X
                    || (candidate.X == current.X && candidateDistance < currentDistance)))))
            ? candidate
            : current;
    }

    private static TemplateCacheKey CreateTemplateCacheKey(ScreenFrame template, int downsampleFactor, CancellationToken cancellationToken, int scaleKey = 0)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var bytesPerPixel = ScreenFrame.GetBytesPerPixel(template.PixelFormat);
        var rowLength = checked(template.Width * bytesPerPixel);
        var content = new byte[checked(rowLength * template.Height)];
        var source = template.Pixels.Span;
        for (var y = 0; y < template.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            source.Slice(checked(y * template.Stride), rowLength).CopyTo(content.AsSpan(y * rowLength, rowLength));
        }

        cancellationToken.ThrowIfCancellationRequested();
        return new TemplateCacheKey(
            template.Width,
            template.Height,
            template.PixelFormat,
            downsampleFactor,
            scaleKey,
            content,
            ComputeContentHash(content));
    }

    private static int ComputeContentHash(ReadOnlySpan<byte> content)
    {
        var hash = new HashCode();
        foreach (var value in content)
        {
            hash.Add(value);
        }

        return hash.ToHashCode();
    }

    private void RemoveTemplateCacheEntry(LinkedListNode<TemplateCacheEntry> node)
    {
        _templateCacheLru.Remove(node);
        _ = _templateCache.Remove(node.Value.Key);
        _templateCacheBytes -= node.Value.SizeBytes;
    }

    private static AnchorPoint[] BuildAnchorPoints(int sampleWidth, int sampleHeight, int downsampleFactor, int requestedCount)
    {
        var sampleCount = checked(sampleWidth * sampleHeight);
        var anchorCount = Math.Min(requestedCount, sampleCount);
        if (anchorCount is 0)
        {
            return [];
        }

        var anchors = new AnchorPoint[anchorCount];
        var previousIndex = -1;
        var uniqueCount = 0;
        for (var anchorIndex = 0; anchorIndex < anchorCount; anchorIndex++)
        {
            var sampleIndex = anchorCount is 1
                ? 0
                : (int)Math.Round(anchorIndex * (sampleCount - 1) / (double)(anchorCount - 1), MidpointRounding.AwayFromZero);
            if (sampleIndex == previousIndex)
            {
                continue;
            }

            previousIndex = sampleIndex;
            var sampleY = sampleIndex / sampleWidth;
            var sampleX = sampleIndex % sampleWidth;
            anchors[uniqueCount++] = new AnchorPoint(sampleX * downsampleFactor, sampleY * downsampleFactor);
        }

        if (uniqueCount == anchors.Length)
        {
            return anchors;
        }

        Array.Resize(ref anchors, uniqueCount);
        return anchors;
    }

    private static bool PassesAnchorPrefilter(
        RgbImage frame,
        RgbImage template,
        ScreenRect frameBounds,
        int candidateX,
        int candidateY,
        ReadOnlySpan<AnchorPoint> anchors,
        long allowedSad,
        CancellationToken cancellationToken)
    {
        long sad = 0;
        foreach (var anchor in anchors)
        {
            cancellationToken.ThrowIfCancellationRequested();
            sad += GetPixelSad(frame, template, frameBounds, candidateX, candidateY, anchor.X, anchor.Y);
            if (sad > allowedSad)
            {
                return false;
            }
        }

        return true;
    }

    private static long? TryComputeSad(
        RgbImage frame,
        RgbImage template,
        ScreenRect frameBounds,
        int candidateX,
        int candidateY,
        int downsampleFactor,
        long allowedSad,
        CancellationToken cancellationToken)
    {
        if (downsampleFactor is 1)
        {
            return TryComputeContiguousSad(frame, template, frameBounds, candidateX, candidateY, allowedSad, cancellationToken);
        }

        long sad = 0;
        for (var templateY = 0; templateY < template.Height; templateY += downsampleFactor)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var templateX = 0; templateX < template.Width; templateX += downsampleFactor)
            {
                sad += GetPixelSad(frame, template, frameBounds, candidateX, candidateY, templateX, templateY);
                if (sad > allowedSad)
                {
                    return null;
                }
            }
        }

        return sad;
    }

    private static long? TryComputeContiguousSad(
        RgbImage frame,
        RgbImage template,
        ScreenRect frameBounds,
        int candidateX,
        int candidateY,
        long allowedSad,
        CancellationToken cancellationToken)
    {
        var frameLocalX = candidateX - frameBounds.X;
        var frameLocalY = candidateY - frameBounds.Y;
        var rowLength = checked(template.Width * ColorChannelCount);
        long sad = 0;
        for (var templateY = 0; templateY < template.Height; templateY++)
        {
            var frameOffset = checked(((frameLocalY + templateY) * frame.RowStride) + (frameLocalX * ColorChannelCount));
            var templateOffset = checked(templateY * template.RowStride);
            var rowSad = TrySumAbsoluteDifferences(
                frame.Pixels.AsSpan(frameOffset, rowLength),
                template.Pixels.AsSpan(templateOffset, rowLength),
                allowedSad - sad,
                cancellationToken);
            if (rowSad is null)
            {
                return null;
            }

            sad += rowSad.Value;
            if (sad > allowedSad)
            {
                return null;
            }
        }

        return sad;
    }

    private static int GetPixelSad(
        RgbImage frame,
        RgbImage template,
        ScreenRect frameBounds,
        int candidateX,
        int candidateY,
        int templateX,
        int templateY)
    {
        var frameLocalX = candidateX - frameBounds.X + templateX;
        var frameLocalY = candidateY - frameBounds.Y + templateY;
        var frameColor = ReadPixel(frame, frameLocalX, frameLocalY);
        var templateColor = ReadPixel(template, templateX, templateY);

        return Math.Abs(frameColor.R - templateColor.R)
            + Math.Abs(frameColor.G - templateColor.G)
            + Math.Abs(frameColor.B - templateColor.B);
    }

    private static RgbImage NormalizeFrame(ScreenFrame frame, CancellationToken cancellationToken)
    {
        var target = new byte[checked(frame.Width * frame.Height * ColorChannelCount)];
        NormalizeFrameInto(frame, target, cancellationToken);
        return new RgbImage(frame.Width, frame.Height, target, checked(frame.Width * ColorChannelCount));
    }

    private static RgbImage NormalizePooledFrame(ScreenFrame frame, CancellationToken cancellationToken)
    {
        var requiredLength = checked(frame.Width * frame.Height * ColorChannelCount);
        var target = ArrayPool<byte>.Shared.Rent(requiredLength);
        try
        {
            NormalizeFrameInto(frame, target, cancellationToken);
            return new RgbImage(frame.Width, frame.Height, target, checked(frame.Width * ColorChannelCount));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            ArrayPool<byte>.Shared.Return(target);
            throw;
        }
    }

    private static void NormalizeFrameInto(ScreenFrame frame, byte[] target, CancellationToken cancellationToken)
    {
        var bytesPerPixel = ScreenFrame.GetBytesPerPixel(frame.PixelFormat);
        var source = frame.Pixels.Span;
        var rowLength = checked(frame.Width * ColorChannelCount);

        if (!ShouldParallelizeRows(frame.Width, frame.Height)
            || !MemoryMarshal.TryGetArray(frame.Pixels, out var sourceSegment)
            || sourceSegment.Array is not { } sourceArray)
        {
            for (var y = 0; y < frame.Height; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (var x = 0; x < frame.Width; x++)
                {
                    var sourceOffset = checked((y * frame.Stride) + (x * bytesPerPixel));
                    var targetOffset = checked((y * rowLength) + (x * ColorChannelCount));
                    WriteNormalizedPixel(source, sourceOffset, frame.PixelFormat, target, targetOffset);
                }
            }
        }
        else
        {
            void NormalizeRow(int y)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sourceRowOffset = checked(sourceSegment.Offset + (y * frame.Stride));
                var targetRowOffset = checked(y * rowLength);
                for (var x = 0; x < frame.Width; x++)
                {
                    var sourceOffset = checked(sourceRowOffset + (x * bytesPerPixel));
                    var targetOffset = checked(targetRowOffset + (x * ColorChannelCount));
                    WriteNormalizedPixel(sourceArray, sourceOffset, frame.PixelFormat, target, targetOffset);
                }
            }

            _ = Parallel.For(
                0,
                frame.Height,
                new ParallelOptions { CancellationToken = cancellationToken },
                NormalizeRow);
        }

    }

    private static bool ShouldParallelizeRows(int width, int height) =>
        width >= MinimumParallelRowWidth
        && height >= MinimumParallelRowCount
        && (long)width * height >= ParallelPixelThreshold;

    private static bool IsRegionValid(ScreenFrame frame, int x, int y, int width, int height)
    {
        return frame.IsRectangleFullyValid(new ScreenRect(x, y, width, height));
    }

    private static void WriteNormalizedPixel(
        ReadOnlySpan<byte> source,
        int sourceOffset,
        ScreenPixelFormat pixelFormat,
        byte[] target,
        int targetOffset)
    {
        switch (pixelFormat)
        {
            case ScreenPixelFormat.Rgb24:
            case ScreenPixelFormat.Abgr8888:
            case ScreenPixelFormat.Xbgr8888:
                target[targetOffset] = source[sourceOffset];
                target[targetOffset + 1] = source[sourceOffset + 1];
                target[targetOffset + 2] = source[sourceOffset + 2];
                break;
            case ScreenPixelFormat.Bgr24:
            case ScreenPixelFormat.Xrgb8888:
            case ScreenPixelFormat.Bgra8888:
                target[targetOffset] = source[sourceOffset + 2];
                target[targetOffset + 1] = source[sourceOffset + 1];
                target[targetOffset + 2] = source[sourceOffset];
                break;
            default:
                throw new InvalidOperationException($"Unsupported screen pixel format '{pixelFormat}'.");
        }
    }

    private static ScreenPixelColor ReadPixel(RgbImage image, int localX, int localY)
    {
        var offset = checked(((localY * image.Width) + localX) * ColorChannelCount);
        return new ScreenPixelColor(image.Pixels[offset], image.Pixels[offset + 1], image.Pixels[offset + 2]);
    }

    private static long? TrySumAbsoluteDifferences(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right, long allowedSad, CancellationToken cancellationToken)
    {
        var sad = 0L;
        var index = 0;
        var nextCancellationCheck = 0;
        if (Vector.IsHardwareAccelerated && left.Length >= Vector<byte>.Count)
        {
            var vectorLength = Vector<byte>.Count;
            var lastVectorStart = left.Length - vectorLength;
            for (; index <= lastVectorStart; index += vectorLength)
            {
                if (index >= nextCancellationCheck)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    nextCancellationCheck = index + CancellationCheckBlockBytes;
                }
                var leftVector = new Vector<byte>(left[index..]);
                var rightVector = new Vector<byte>(right[index..]);
                var differences = Vector.Max(leftVector, rightVector) - Vector.Min(leftVector, rightVector);
                sad += SumByteVector(differences);
                if (sad > allowedSad)
                {
                    return null;
                }
            }
        }

        for (; index < left.Length; index++)
        {
            if (index >= nextCancellationCheck)
            {
                cancellationToken.ThrowIfCancellationRequested();
                nextCancellationCheck = index + CancellationCheckBlockBytes;
            }
            sad += Math.Abs(left[index] - right[index]);
            if (sad > allowedSad)
            {
                return null;
            }
        }

        return sad;
    }

    private static long CalculateAllowedSad(double maximumSad, double minimumSimilarity)
    {
        return checked((long)Math.Floor(maximumSad * (1.0 - minimumSimilarity)));
    }

    private static uint SumByteVector(Vector<byte> vector)
    {
        Vector.Widen(vector, out Vector<ushort> lower, out Vector<ushort> upper);
        return SumUshortVector(lower) + SumUshortVector(upper);
    }

    private static uint SumUshortVector(Vector<ushort> vector)
    {
        Vector.Widen(vector, out Vector<uint> lower, out Vector<uint> upper);
        return SumUIntVector(lower) + SumUIntVector(upper);
    }

    private static uint SumUIntVector(Vector<uint> vector)
    {
        var sum = 0U;
        for (var index = 0; index < Vector<uint>.Count; index++)
        {
            sum += vector[index];
        }

        return sum;
    }

    private static double CalculateScore(long sad, double maximumSad)
    {
        if (maximumSad <= 0.0)
        {
            return 1.0;
        }

        return Math.Clamp(1.0 - (sad / maximumSad), 0.0, 1.0);
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private readonly record struct AnchorPoint(int X, int Y);

    private readonly record struct RgbImage(int Width, int Height, byte[] Pixels, int RowStride);

    private readonly record struct TemplateCacheKey(
        int Width,
        int Height,
        ScreenPixelFormat PixelFormat,
        int DownsampleFactor,
        int ScaleKey,
        byte[] Content,
        int ContentHash);

    private sealed class TemplateCacheKeyComparer : IEqualityComparer<TemplateCacheKey>
    {
        public static TemplateCacheKeyComparer Instance { get; } = new();

        public bool Equals(TemplateCacheKey left, TemplateCacheKey right)
        {
            return left.Width == right.Width
                && left.Height == right.Height
                && left.PixelFormat == right.PixelFormat
                && left.DownsampleFactor == right.DownsampleFactor
                && left.ScaleKey == right.ScaleKey
                && left.ContentHash == right.ContentHash
                && left.Content.AsSpan().SequenceEqual(right.Content);
        }

        public int GetHashCode(TemplateCacheKey key)
        {
            return HashCode.Combine(
                key.Width,
                key.Height,
                key.PixelFormat,
                key.DownsampleFactor,
                key.ScaleKey,
                key.ContentHash);
        }
    }

    private sealed class TemplateCacheEntry(ScreenImageMatcher.TemplateCacheKey key, ScreenImageMatcher.RgbImage image, long sizeBytes)
    {
        public TemplateCacheKey Key { get; } = key;

        public RgbImage Image { get; } = image;

        public long SizeBytes { get; } = sizeBytes;
    }

    private sealed class MatcherSearchLease(ScreenImageMatcher owner) : IDisposable
    {
        private ScreenImageMatcher? _owner = owner;

        public void Dispose()
        {
            Interlocked.Exchange(ref _owner, value: null)?.ExitSearchLease();
        }
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private readonly record struct MatchCandidate(int X, int Y, long Sad, double MaximumSad = 0, int Width = 0, int Height = 0, double Scale = 1.0)
    {
        public static MatchCandidate None => new(0, 0, long.MaxValue);

        public bool HasValue => Sad != long.MaxValue;
    }

    private static MatchCandidate BetterOf(MatchCandidate current, MatchCandidate candidate)
    {
        if (!candidate.HasValue)
        {
            return current;
        }

        if (!current.HasValue
            || candidate.Sad < current.Sad
            || (candidate.Sad == current.Sad && (candidate.Y < current.Y || (candidate.Y == current.Y && candidate.X < current.X))))
        {
            return candidate;
        }

        return current;
    }

    private static MatchCandidate EarlierOf(MatchCandidate current, MatchCandidate candidate)
    {
        if (!candidate.HasValue)
        {
            return current;
        }

        if (!current.HasValue
            || candidate.Y < current.Y
            || (candidate.Y == current.Y && candidate.X < current.X))
        {
            return candidate;
        }

        return current;
    }

    private struct EarlySuccessSignal
    {
        public bool IsRequested { get; private set; }

        public void Request() => IsRequested = true;
    }
}
