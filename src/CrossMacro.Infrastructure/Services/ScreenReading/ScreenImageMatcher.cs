
namespace CrossMacro.Infrastructure.Services.ScreenReading;

public sealed class ScreenImageMatcher : IDisposable
{
    private const int ColorChannelCount = 3;
    private const int MaxChannelDifference = byte.MaxValue;
    private const int CancellationCheckBlockBytes = 4096;
    private const long ParallelPixelThreshold = 256_000;
    private const int MinimumParallelRowWidth = 256;
    private const int MinimumParallelRowCount = 4;
    private const int MaximumParallelism = 4;
    private const double ScaleScoreTieTolerance = 1e-12;
    private const long AutomaticDirectScanWork = 4_000_000;
    private const int AutomaticCandidateLimit = 8;
    private const double AutomaticMinimumEvidenceMargin = 0.02;
    private const int AutomaticSameTargetCenterTolerance = 2;
    private const double MinimumAutomaticAppearanceEffectivePixels = 4.0;
    private const double MaximumAutomaticPhotometricOffset = 64.0;
    private const int MinimumSparseTemplateArea = 16;
    private const int MinimumAutomaticScaledTemplateExtent = 4;
    private const int PyramidRefinementRadius = 3;
    // Bound automatic pyramid setup on large desktops.
    private const int MaximumPyramidLevels = 6;
    private const int MinimumPyramidTemplateExtent = 4;
    internal const long MaxMatcherWork = 100_000_000;
    // Bound setup separately from candidate comparisons.
    internal const long MaxMatcherPreparationWork = 1_000_000_000;
    private const int MatcherRowBandHeight = 32;
    internal const long MaxTemplateCacheBytes = 64L * 1024 * 1024;
    private static readonly double[] AutomaticCoarseScales = [0.70, 0.80, 0.90, 1.10, 1.20, 1.30, 1.35, 1.50];
    private static readonly double[] LocalScaleOffsets = [-0.02, -0.01, 0.01, 0.02];

    private readonly Lock _lifetimeLock = new();
    private readonly Lock _templateCacheLock = new();
    private readonly Lock _templateMaterializationLock = new();
    private readonly ManualResetEventSlim _searchesCompleted = new(initialState: false);
    private readonly ManualResetEventSlim _disposeCompleted = new(initialState: false);
    private readonly Dictionary<TemplateCacheKey, LinkedListNode<TemplateCacheEntry>> _templateCache = new(TemplateCacheKeyComparer.Instance);
    private readonly LinkedList<TemplateCacheEntry> _templateCacheLru = new();
    private readonly long _maxTemplateCacheBytes;
    private long _templateCacheBytes;
    private int _templateNormalizationCount;
    private int _templatePyramidBuildCount;
    private long _lastAutomaticSearchWork;
    private long _lastAutomaticCandidateWork;
    private long _lastAutomaticPreparationWork;
    private long _lastDeterministicCandidateWork;
    private int _lastAutomaticCandidateCount;
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

    // Test-only deterministic diagnostics.
    internal int TemplatePyramidBuildCount => Volatile.Read(ref _templatePyramidBuildCount);

    internal (long Work, int CandidateCount, long CandidateWork, long PreparationWork) LastAutomaticSearchDiagnostics =>
        (Volatile.Read(ref _lastAutomaticSearchWork),
            Volatile.Read(ref _lastAutomaticCandidateCount),
            Volatile.Read(ref _lastAutomaticCandidateWork),
            Volatile.Read(ref _lastAutomaticPreparationWork));

    internal long LastDeterministicCandidateWork => Volatile.Read(ref _lastDeterministicCandidateWork);

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

        if (options.SelectionMode is not ScreenImageMatchSelectionMode.Automatic
            && (template.Width > region.Width || template.Height > region.Height))
        {
            return null;
        }

        if (options.SelectionMode is ScreenImageMatchSelectionMode.Automatic)
        {
            return FindAutomaticMatchWithPooledFrame(frame, template, region, options, cancellationToken);
        }

        if (!IsDeterministicSearchWithinWorkBudget(region, template, options.AnchorPointCount, options.SelectionMode))
        {
            return FindAutomaticMatchWithPooledFrame(frame, template, region, options, cancellationToken);
        }

        try
        {
            return FindDeterministicMatch(frame, template, region, options, cancellationToken);
        }
        catch (ScreenImageMatcherResourceLimitException exception) when (!exception.IsPreparationLimit)
        {
            return FindAutomaticMatchWithPooledFrame(frame, template, region, options, cancellationToken);
        }
    }

    private static bool IsDeterministicSearchWithinWorkBudget(
        ScreenRect region,
        ScreenFrame template,
        int anchorPointCount,
        ScreenImageMatchSelectionMode selectionMode)
    {
        var candidateWidth = checked((long)region.Width - template.Width + 1);
        var candidateHeight = checked((long)region.Height - template.Height + 1);
        var candidateCount = SaturatingMultiply(candidateWidth, candidateHeight);
        var templatePixels = SaturatingMultiply(template.Width, template.Height);
        var anchors = Math.Min((long)Math.Max(0, anchorPointCount), templatePixels);
        var candidateWork = SaturatingMultiply(SaturatingAdd(templatePixels, anchors), ColorChannelCount);
        var totalWork = SaturatingMultiply(candidateCount, candidateWork);
        if (selectionMode is ScreenImageMatchSelectionMode.BestMatch)
        {
            totalWork = SaturatingMultiply(totalWork, 2);
        }

        return totalWork <= MaxMatcherWork;
    }

    private ScreenImageMatch? FindDeterministicMatch(
        ScreenFrame frame,
        ScreenFrame template,
        ScreenRect region,
        ScreenImageMatchOptions options,
        CancellationToken cancellationToken)
    {
        var sampleWidth = template.Width;
        var sampleHeight = template.Height;
        var samplePixelCount = SaturatingMultiply(sampleWidth, sampleHeight);
        if (samplePixelCount > MaxMatcherWork / ColorChannelCount)
        {
            var requestedWork = SaturatingMultiply(samplePixelCount, ColorChannelCount);
            throw new ScreenImageMatcherResourceLimitException(
                requestedWork,
                MaxMatcherWork,
                $"A single image matcher candidate requires more than {MaxMatcherWork.ToString("N0", CultureInfo.InvariantCulture)} channel comparisons, exceeding the internal limit.");
        }

        var requestedAnchorCount = Math.Min((long)options.AnchorPointCount, samplePixelCount);
        if (SaturatingAdd(samplePixelCount, requestedAnchorCount) > MaxMatcherWork / ColorChannelCount)
        {
            var requestedWork = SaturatingMultiply(SaturatingAdd(samplePixelCount, requestedAnchorCount), ColorChannelCount);
            throw new ScreenImageMatcherResourceLimitException(
                requestedWork,
                MaxMatcherWork,
                $"A single image matcher candidate, including its requested prefilter, requires {requestedWork.ToString("N0", CultureInfo.InvariantCulture)} channel comparisons, exceeding the internal limit of {MaxMatcherWork.ToString("N0", CultureInfo.InvariantCulture)}.");
        }

        var anchors = BuildAnchorPoints(sampleWidth, sampleHeight, options.AnchorPointCount);
        var singleCandidateWork = SaturatingMultiply(SaturatingAdd(samplePixelCount, anchors.LongLength), ColorChannelCount);
        if (singleCandidateWork > MaxMatcherWork)
        {
            throw new ScreenImageMatcherResourceLimitException(
                singleCandidateWork,
                MaxMatcherWork,
                $"A single image matcher candidate, including its prefilter, requires {singleCandidateWork.ToString("N0", CultureInfo.InvariantCulture)} channel comparisons, exceeding the internal limit of {MaxMatcherWork.ToString("N0", CultureInfo.InvariantCulture)}.");
        }

        var budget = new SearchBudget(MaxMatcherWork);
        budget.ConsumePreparation(EstimatePixelWork(region.Width, region.Height));
        var framePixels = NormalizePooledFrame(frame, region, cancellationToken);
        try
        {
            var preparedTemplate = GetPreparedTemplate(template, options.UseTemplateAlphaMask, options.AlphaThreshold, budget, cancellationToken);
            var templatePixels = preparedTemplate.Image;
            var effectivePixelCount = CountEffectivePixels(templatePixels);
            if (effectivePixelCount is 0)
            {
                throw new ArgumentException("The template does not contain any pixels above its alpha threshold.", nameof(template));
            }
            var maximumSad = preparedTemplate.Statistics.MaximumSad;
            var allowedSad = CalculateAllowedSad(maximumSad, options.MinimumSimilarity);

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
                    region,
                    region.X,
                    checked((long)region.X + candidateWidth),
                    startY,
                    endY,
                    anchors,
                    allowedSad,
                    options.SelectionMode,
                    budget,
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
                    if (selectedCandidate.Sad is 0)
                    {
                        break;
                    }
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
            Volatile.Write(ref _lastDeterministicCandidateWork, budget.ConsumedSearchWork);
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
        ScreenImageMatchSelectionMode selectionMode,
        SearchBudget budget,
        CancellationToken cancellationToken)
    {
        var frameFullyValid = validityFrame.IsRectangleFullyValid(frameBounds);
        if (selectionMode is ScreenImageMatchSelectionMode.BestMatch
            && template.Width >= 16
            && template.Height >= 16
            && frameFullyValid
            && template.AlphaMask is null)
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
                budget,
                cancellationToken);

            if (result.HasValue)
            {
                var fullSearch = FindBestCandidateStandard(
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
                    budget,
                    cancellationToken);
                return BetterOf(result, fullSearch);
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
            selectionMode,
            budget,
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
        ScreenImageMatchSelectionMode selectionMode,
        SearchBudget budget,
        CancellationToken cancellationToken)
    {
        var bestCandidate = MatchCandidate.None;
        var bestCandidateLock = new Lock();
        var frameFullyValid = validityFrame.IsRectangleFullyValid(frameBounds);
        var parallelOptions = CreateParallelOptions(cancellationToken);
        try
        {
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
                    selectionMode,
                    budget,
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
        }
        catch (AggregateException exception)
        {
            RethrowResourceLimit(exception);
            throw;
        }

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
        SearchBudget budget,
        CancellationToken cancellationToken)
    {
        int minX = (int)startX;
        int maxX = (int)(endX + template.Width - 2);
        int minY = (int)startY;
        int maxY = (int)(endY + template.Height - 2);
        int regionW = maxX - minX + 1;
        int regionH = maxY - minY + 1;

        if (regionW < template.Width || regionH < template.Height)
        {
            return MatchCandidate.None;
        }

        budget.ConsumePreparation(EstimateResamplingWork(template.Width, template.Height, sourcePixelCountPerOutput: 4));
        budget.ConsumePreparation(EstimateResamplingWork(regionW, regionH, sourcePixelCountPerOutput: 4));

        var templateDown = DownsampleBy2(template, cancellationToken);

        var frameLocalX = checked(minX - frameBounds.X);
        var frameLocalY = checked(minY - frameBounds.Y);
        var frameDown = CropAndDownsampleBy2(frame, frameLocalX, frameLocalY, regionW, regionH, cancellationToken);

        const int startXDown = 0;
        int endXDown = frameDown.Width - templateDown.Width + 1;
        int endYDown = frameDown.Height - templateDown.Height + 1;

        if (endXDown <= 0 || endYDown <= 0)
        {
            return MatchCandidate.None;
        }

        var anchorsDown = BuildAnchorPoints(templateDown.Width, templateDown.Height, anchors.Length);

        double targetSimilarity = 1.0 - ((double)allowedSad / ((double)template.Width * template.Height * ColorChannelCount * MaxChannelDifference));
        double coarseSimilarity = Math.Max(0.5, targetSimilarity - 0.15); // Lower similarity threshold by 15% for decimation and phase shift margin
        double maximumSadDown = (double)templateDown.Width * templateDown.Height * ColorChannelCount * MaxChannelDifference;
        long allowedSadDown = CalculateAllowedSad(maximumSadDown, coarseSimilarity);

        var bestCandidate = MatchCandidate.None;
        var bestCandidateLock = new Lock();
        var parallelOptions = CreateParallelOptions(cancellationToken);

        try
        {
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
                    ScreenImageMatchSelectionMode.BestMatch,
                    budget,
                    cancellationToken);

                if (rowBestDown.HasValue)
                {
                    lock (bestCandidateLock)
                    {
                        bestCandidate = BetterOf(bestCandidate, rowBestDown);
                    }
                }
            });
        }
        catch (AggregateException exception)
        {
            RethrowResourceLimit(exception);
            throw;
        }

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
            selectionMode,
            budget,
            cancellationToken);
    }

    private static RgbImage DownsampleBy2(RgbImage source, CancellationToken cancellationToken)
    {
        int w = Math.Max(1, (source.Width + 1) / 2);
        int h = Math.Max(1, (source.Height + 1) / 2);
        byte[] pixels = new byte[w * h * ColorChannelCount];
        byte[]? alphaMask = source.AlphaMask is null ? null : new byte[w * h];

        void CopyRow(int y)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int sourceY = y * 2;
            int targetRowOffset = y * w * ColorChannelCount;
            for (int x = 0; x < w; x++)
            {
                int sourceX = x * 2;
                var sourceXEnd = Math.Min(source.Width, sourceX + 2);
                var sourceYEnd = Math.Min(source.Height, sourceY + 2);
                var sourcePixelCount = 0;
                long coverageSum = 0;
                long red = 0;
                long green = 0;
                long blue = 0;
                for (var currentY = sourceY; currentY < sourceYEnd; currentY++)
                {
                    for (var currentX = sourceX; currentX < sourceXEnd; currentX++)
                    {
                        var sourceOffset = (currentY * source.RowStride) + (currentX * ColorChannelCount);
                        var coverage = source.AlphaMask is null
                            ? byte.MaxValue
                            : source.AlphaMask[(currentY * source.Width) + currentX];
                        sourcePixelCount++;
                        if (coverage is not 0)
                        {
                            red += source.Pixels[sourceOffset] * (long)coverage;
                            green += source.Pixels[sourceOffset + 1] * (long)coverage;
                            blue += source.Pixels[sourceOffset + 2] * (long)coverage;
                            coverageSum += coverage;
                        }
                    }
                }

                int targetOffset = targetRowOffset + (x * ColorChannelCount);
                if (coverageSum is not 0)
                {
                    pixels[targetOffset] = (byte)((red + (coverageSum / 2)) / coverageSum);
                    pixels[targetOffset + 1] = (byte)((green + (coverageSum / 2)) / coverageSum);
                    pixels[targetOffset + 2] = (byte)((blue + (coverageSum / 2)) / coverageSum);
                }

                if (alphaMask is { } mask)
                {
                    mask[(y * w) + x] = sourcePixelCount is 0
                        ? (byte)0
                        : (byte)((coverageSum + (sourcePixelCount / 2)) / sourcePixelCount);
                }
            }
        }

        if (ShouldParallelizeRows(w, h))
        {
            _ = Parallel.For(0, h, CreateParallelOptions(cancellationToken), CopyRow);
        }
        else
        {
            for (int y = 0; y < h; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CopyRow(y);
            }
        }

        return CreateRgbImage(w, h, pixels, alphaMask);
    }

    private static RgbImage CropAndDownsampleBy2(
        RgbImage source,
        int startX,
        int startY,
        int width,
        int height,
        CancellationToken cancellationToken)
    {
        int w = Math.Max(1, (width + 1) / 2);
        int h = Math.Max(1, (height + 1) / 2);
        byte[] pixels = new byte[w * h * ColorChannelCount];
        byte[]? alphaMask = source.AlphaMask is null ? null : new byte[w * h];

        void CopyRow(int y)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int sourceY = startY + (y * 2);
            int targetRowOffset = y * w * ColorChannelCount;
            for (int x = 0; x < w; x++)
            {
                int sourceX = startX + (x * 2);
                var sourceXEnd = Math.Min(startX + width, sourceX + 2);
                var sourceYEnd = Math.Min(startY + height, sourceY + 2);
                var sourcePixelCount = 0;
                long coverageSum = 0;
                long red = 0;
                long green = 0;
                long blue = 0;
                for (var currentY = sourceY; currentY < sourceYEnd; currentY++)
                {
                    for (var currentX = sourceX; currentX < sourceXEnd; currentX++)
                    {
                        var sourceOffset = (currentY * source.RowStride) + (currentX * ColorChannelCount);
                        var coverage = source.AlphaMask is null
                            ? byte.MaxValue
                            : source.AlphaMask[(currentY * source.Width) + currentX];
                        sourcePixelCount++;
                        if (coverage is not 0)
                        {
                            red += source.Pixels[sourceOffset] * (long)coverage;
                            green += source.Pixels[sourceOffset + 1] * (long)coverage;
                            blue += source.Pixels[sourceOffset + 2] * (long)coverage;
                            coverageSum += coverage;
                        }
                    }
                }

                int targetOffset = targetRowOffset + (x * ColorChannelCount);
                if (coverageSum is not 0)
                {
                    pixels[targetOffset] = (byte)((red + (coverageSum / 2)) / coverageSum);
                    pixels[targetOffset + 1] = (byte)((green + (coverageSum / 2)) / coverageSum);
                    pixels[targetOffset + 2] = (byte)((blue + (coverageSum / 2)) / coverageSum);
                }

                if (alphaMask is { } mask)
                {
                    mask[(y * w) + x] = sourcePixelCount is 0
                        ? (byte)0
                        : (byte)((coverageSum + (sourcePixelCount / 2)) / sourcePixelCount);
                }
            }
        }

        if (ShouldParallelizeRows(w, h))
        {
            _ = Parallel.For(0, h, CreateParallelOptions(cancellationToken), CopyRow);
        }
        else
        {
            for (int y = 0; y < h; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CopyRow(y);
            }
        }

        return CreateRgbImage(w, h, pixels, alphaMask);
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
        ScreenImageMatchSelectionMode selectionMode,
        SearchBudget budget,
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
            if (!frameFullyValid && !HasValidTemplateCoverage(validityFrame, template, candidateX, candidateY))
            {
                continue;
            }

            budget.Consume(EstimateCandidateWork(template, anchors.Length));

            var candidateLimit = selectionMode is ScreenImageMatchSelectionMode.BestMatch && rowBest.HasValue
                ? Math.Min(allowedSad, rowBest.Sad)
                : allowedSad;
            if (!PassesAnchorPrefilter(frame, template, frameBounds, candidateX, candidateY, anchors, candidateLimit, cancellationToken))
            {
                continue;
            }

            var sad = TryComputeSad(frame, template, frameBounds, candidateX, candidateY, candidateLimit, cancellationToken);
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

    private PreparedTemplate GetPreparedTemplate(
        ScreenFrame template,
        bool useAlphaMask,
        byte alphaThreshold,
        SearchBudget budget,
        CancellationToken cancellationToken,
        TemplateCacheContent? cacheContent = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_templateMaterializationLock)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var content = cacheContent ?? CreateTemplateCacheContent(template, budget, cancellationToken);
            var key = CreateTemplateCacheKey(template, useAlphaMask, alphaThreshold, content, cancellationToken);
            lock (_templateCacheLock)
            {
                if (_templateCache.TryGetValue(key, out var cached))
                {
                    _templateCacheLru.Remove(cached);
                    _templateCacheLru.AddFirst(cached);
                    return new PreparedTemplate(key, cached.Value.Image, cached.Value.Statistics);
                }
            }

            budget.ConsumePreparation(EstimatePixelWork(template.Width, template.Height, includeAlpha: useAlphaMask));
            var normalized = NormalizeFrame(template, useAlphaMask, alphaThreshold, cancellationToken);
            if (normalized.EffectivePixelCount is 0)
            {
                throw new ArgumentException("The template does not contain any pixels above its alpha threshold.", nameof(template));
            }
            var statistics = TemplateStatistics.Create(normalized);
            _ = Interlocked.Increment(ref _templateNormalizationCount);
            var entryBytes = checked((long)key.Content.Bytes.Length + normalized.Pixels.LongLength + (normalized.AlphaMask?.LongLength ?? 0));
            if (entryBytes > _maxTemplateCacheBytes)
            {
                return new PreparedTemplate(key, normalized, statistics);
            }

            lock (_templateCacheLock)
            {
                if (_templateCache.TryGetValue(key, out var cached))
                {
                    _templateCacheLru.Remove(cached);
                    _templateCacheLru.AddFirst(cached);
                    return new PreparedTemplate(key, cached.Value.Image, cached.Value.Statistics);
                }

                while (_templateCacheBytes + entryBytes > _maxTemplateCacheBytes && _templateCacheLru.Last is { } oldest)
                {
                    RemoveTemplateCacheEntry(oldest);
                }

                var entry = new TemplateCacheEntry(key, normalized, statistics, entryBytes);
                var node = _templateCacheLru.AddFirst(entry);
                _templateCache.Add(key, node);
                _templateCacheBytes += entryBytes;
                return new PreparedTemplate(key, normalized, statistics);
            }
        }
    }

    private ScreenImageMatch? FindAutomaticMatchWithPooledFrame(
        ScreenFrame frame,
        ScreenFrame template,
        ScreenRect region,
        ScreenImageMatchOptions options,
        CancellationToken cancellationToken)
    {
        var budget = new SearchBudget(MaxMatcherWork, MaxMatcherPreparationWork);
        budget.ConsumePreparation(EstimatePixelWork(region.Width, region.Height));
        var framePixels = NormalizePooledFrame(frame, region, cancellationToken);
        List<AutomaticCandidate>? allCandidates = null;
        var framePyramidCache = new AutomaticFramePyramidCache();
        try
        {
            var cacheContent = CreateTemplateCacheContent(template, budget, cancellationToken);
            var nativeTemplate = GetPreparedTemplate(
                template,
                options.UseTemplateAlphaMask,
                options.AlphaThreshold,
                budget,
                cancellationToken,
                cacheContent);
            if (HasInsufficientAutomaticAlphaEvidence(nativeTemplate))
            {
                return null;
            }

            allCandidates = new List<AutomaticCandidate>(capacity: 16);
            var observedDimensions = new HashSet<(int Width, int Height)>();
            var requireDistinctEvidence = options.SelectionMode is ScreenImageMatchSelectionMode.Automatic;

            try
            {
                EvaluateAutomaticScale(
                    frame,
                    framePixels,
                    nativeTemplate,
                    region,
                    options,
                    scale: 1.0,
                    allCandidates,
                    framePyramidCache,
                    budget,
                    cancellationToken);
                observedDimensions.Add((nativeTemplate.Image.Width, nativeTemplate.Image.Height));

                var nativeEvidence = MatchEvidence.Create(allCandidates);
                if (nativeEvidence.IsExactNativeMatch(options.MinimumSimilarity, requireDistinctEvidence))
                {
                    return nativeEvidence.ToMatch(options.MinimumSimilarity, requireDistinctEvidence);
                }

                var scaleEvidence = new List<ScaleEvidence>();
                foreach (var scale in AutomaticCoarseScales)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var width = Math.Max(1, (int)Math.Round(template.Width * scale, MidpointRounding.AwayFromZero));
                    var height = Math.Max(1, (int)Math.Round(template.Height * scale, MidpointRounding.AwayFromZero));
                    if (width < MinimumAutomaticScaledTemplateExtent
                        || height < MinimumAutomaticScaledTemplateExtent
                        || width > region.Width
                        || height > region.Height
                        || !observedDimensions.Add((width, height)))
                    {
                        continue;
                    }

                    var scaled = GetScaledPreparedTemplate(
                        template,
                        nativeTemplate.Image,
                        cacheContent,
                        width,
                        height,
                        scale,
                        options.UseTemplateAlphaMask,
                        options.AlphaThreshold,
                        budget,
                        cancellationToken);
                    var before = allCandidates.Count;
                    EvaluateAutomaticScale(frame, framePixels, scaled, region, options, scale, allCandidates, framePyramidCache, budget, cancellationToken);
                    scaleEvidence.Add(new ScaleEvidence(scale, allCandidates.Skip(before).ToArray()));
                }

                foreach (var scale in scaleEvidence
                    .OrderByDescending(static evidence => evidence.BestScore)
                    .ThenBy(static evidence => Math.Abs(evidence.Scale - 1.0))
                    .Take(2)
                    .SelectMany(static evidence => LocalScaleOffsets.Select(offset => evidence.Scale * (1.0 + offset))))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (scale is < 0.70 or > 1.50)
                    {
                        continue;
                    }

                    var width = Math.Max(1, (int)Math.Round(template.Width * scale, MidpointRounding.AwayFromZero));
                    var height = Math.Max(1, (int)Math.Round(template.Height * scale, MidpointRounding.AwayFromZero));
                    if (width < MinimumAutomaticScaledTemplateExtent
                        || height < MinimumAutomaticScaledTemplateExtent
                        || width > region.Width
                        || height > region.Height
                        || !observedDimensions.Add((width, height)))
                    {
                        continue;
                    }

                    var scaled = GetScaledPreparedTemplate(
                        template,
                        nativeTemplate.Image,
                        cacheContent,
                        width,
                        height,
                        scale,
                        options.UseTemplateAlphaMask,
                        options.AlphaThreshold,
                        budget,
                        cancellationToken);
                    EvaluateAutomaticScale(frame, framePixels, scaled, region, options, scale, allCandidates, framePyramidCache, budget, cancellationToken);
                }

                return MatchEvidence.Create(allCandidates).ToMatch(options.MinimumSimilarity, requireDistinctEvidence);
            }
            catch (ScreenImageMatcherResourceLimitException exception) when (!exception.IsPreparationLimit)
            {
                return MatchEvidence.Create(allCandidates).ToMatch(options.MinimumSimilarity, requireDistinctEvidence);
            }
        }
        finally
        {
            Volatile.Write(ref _lastAutomaticSearchWork, budget.ConsumedWork);
            Volatile.Write(ref _lastAutomaticCandidateWork, budget.ConsumedSearchWork);
            Volatile.Write(ref _lastAutomaticPreparationWork, budget.ConsumedPreparationWork);
            Volatile.Write(ref _lastAutomaticCandidateCount, allCandidates?.Count ?? 0);
            ArrayPool<byte>.Shared.Return(framePixels.Pixels);
        }
    }

    private void EvaluateAutomaticScale(
        ScreenFrame validityFrame,
        RgbImage frame,
        PreparedTemplate preparedTemplate,
        ScreenRect region,
        ScreenImageMatchOptions options,
        double scale,
        List<AutomaticCandidate> destination,
        AutomaticFramePyramidCache framePyramidCache,
        SearchBudget budget,
        CancellationToken cancellationToken)
    {
        var template = preparedTemplate.Image;
        var statistics = preparedTemplate.Statistics;
        if (statistics.CoverageSum is 0)
        {
            return;
        }

        var maximumSad = statistics.MaximumSad;
        var allowedSad = statistics.HasUsableVariance
            ? CalculateAllowedSad(maximumSad, options.MinimumSimilarity)
            : 0;
        var candidateCount = checked((long)(frame.Width - template.Width + 1) * (frame.Height - template.Height + 1));
        if (candidateCount <= 0)
        {
            return;
        }

        var fullScanWork = EstimateAutomaticScanWork(candidateCount, template, correlation: false);
        var isSmallSearch = fullScanWork <= AutomaticDirectScanWork;
        AutomaticSearchResult result;
        if (isSmallSearch)
        {
            result = new AutomaticSearchResult(
                ScanWeightedCandidates(
                    frame,
                    validityFrame,
                    region,
                    template,
                    allowedSad,
                    maximumSad,
                    limit: AutomaticCandidateLimit,
                    budget,
                    cancellationToken),
                CoarseEvidenceSufficient: true);
        }
        else
        {
            result = FindPyramidCandidates(frame, validityFrame, region, preparedTemplate, allowedSad, maximumSad, options.MinimumSimilarity, framePyramidCache, budget, cancellationToken);
        }

        var correlationAttempted = false;
        if (statistics.HasUsableVariance
            && (result.Candidates.Count is 0 || (!isSmallSearch && !result.CoarseEvidenceSufficient)))
        {
            correlationAttempted = true;
            var correlationCandidates = isSmallSearch
                ? FindCorrelationCandidates(
                    frame,
                    validityFrame,
                    region,
                    template,
                    statistics,
                    options.MinimumSimilarity,
                    limit: AutomaticCandidateLimit,
                    budget,
                    cancellationToken)
                : FindPyramidCorrelationCandidates(
                    frame,
                    validityFrame,
                    region,
                    preparedTemplate,
                    statistics,
                    options.MinimumSimilarity,
                    framePyramidCache,
                    budget,
                    cancellationToken);
            if (correlationCandidates.Count > 0)
            {
                result = result with
                {
                    Candidates = correlationCandidates,
                    CoarseEvidenceSufficient = true,
                };
            }
        }

        if (!isSmallSearch && !result.CoarseEvidenceSufficient && budget.CanConsume(fullScanWork))
        {
            result = result with
            {
                Candidates = ScanWeightedCandidates(
                    frame,
                    validityFrame,
                    region,
                    template,
                    allowedSad,
                    maximumSad,
                    limit: AutomaticCandidateLimit,
                    budget,
                    cancellationToken),
                CoarseEvidenceSufficient = true,
            };
        }

        if (result.Candidates.Count is 0 && statistics.HasUsableVariance && !correlationAttempted)
        {
            var correlationCandidates = FindCorrelationCandidates(
                frame,
                validityFrame,
                region,
                template,
                statistics,
                options.MinimumSimilarity,
                limit: AutomaticCandidateLimit,
                budget,
                cancellationToken);
            result = result with { Candidates = correlationCandidates };
        }

        foreach (var candidate in result.Candidates)
        {
            destination.Add(candidate with
            {
                X = checked(region.X + candidate.X),
                Y = checked(region.Y + candidate.Y),
                Scale = scale,
                Width = template.Width,
                Height = template.Height,
                Coverage = statistics.Coverage,
                EffectivePixels = statistics.EffectivePixelCount,
            });
        }
    }

    private static bool HasInsufficientAutomaticAlphaEvidence(PreparedTemplate preparedTemplate)
    {
        var template = preparedTemplate.Image;
        return template.AlphaMask is not null
            && checked((long)template.Width * template.Height) >= MinimumSparseTemplateArea
            && preparedTemplate.Statistics.EffectivePixelCount < MinimumAutomaticAppearanceEffectivePixels;
    }

    private AutomaticSearchResult FindPyramidCandidates(
        RgbImage frame,
        ScreenFrame validityFrame,
        ScreenRect region,
        PreparedTemplate preparedTemplate,
        long allowedSad,
        double maximumSad,
        double minimumSimilarity,
        AutomaticFramePyramidCache framePyramidCache,
        SearchBudget budget,
        CancellationToken cancellationToken)
    {
        var template = preparedTemplate.Image;
        var templatePyramid = GetTemplatePyramid(preparedTemplate, budget, cancellationToken);
        var framePyramid = framePyramidCache.Get(frame, templatePyramid.Count, budget, cancellationToken);
        var levels = Math.Min(framePyramid.Count, templatePyramid.Count);
        if (levels < 2)
        {
            return new AutomaticSearchResult([], CoarseEvidenceSufficient: false);
        }

        var level = levels - 1;
        var coarseFrame = framePyramid[level];
        var coarseTemplate = templatePyramid[level];
        if (coarseTemplate.Width > coarseFrame.Width || coarseTemplate.Height > coarseFrame.Height)
        {
            return new AutomaticSearchResult([], CoarseEvidenceSufficient: false);
        }

        var coarseMaximumSad = TemplateStatistics.Create(coarseTemplate).MaximumSad;
        var coarse = ScanWeightedCandidates(
            coarseFrame,
            validityFrame: null,
            validityOrigin: default,
            coarseTemplate,
            allowedSad: long.MaxValue,
            coarseMaximumSad,
            limit: AutomaticCandidateLimit,
            budget,
            cancellationToken);
        var coarseEvidenceSufficient = coarse.Count > 0
            && coarse[0].Score >= Math.Max(0.50, minimumSimilarity - 0.25);
        var positions = ApplyNonMaximumSuppression(coarse, coarseTemplate.Width, coarseTemplate.Height, AutomaticCandidateLimit);
        positions = RefinePyramidPositions(framePyramid, templatePyramid, positions, level, budget, cancellationToken);

        var accepted = new List<AutomaticCandidate>(capacity: 2);
        foreach (var position in positions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sad = TryComputeWeightedSad(frame, validityFrame, region, template, position.X, position.Y, allowedSad, cancellationToken);
            if (sad is { } value)
            {
                AddAutomaticCandidate(
                    accepted,
                    new AutomaticCandidate(position.X, position.Y, CalculateScore(value, maximumSad), "weighted-sad"),
                    limit: 2);
            }
        }

        return new AutomaticSearchResult(accepted, coarseEvidenceSufficient && accepted.Count > 0);
    }

    private List<AutomaticCandidate> FindPyramidCorrelationCandidates(
        RgbImage frame,
        ScreenFrame validityFrame,
        ScreenRect region,
        PreparedTemplate preparedTemplate,
        TemplateStatistics statistics,
        double minimumSimilarity,
        AutomaticFramePyramidCache framePyramidCache,
        SearchBudget budget,
        CancellationToken cancellationToken)
    {
        var template = preparedTemplate.Image;
        var templatePyramid = GetTemplatePyramid(preparedTemplate, budget, cancellationToken);
        var framePyramid = framePyramidCache.Get(frame, templatePyramid.Count, budget, cancellationToken);
        var levels = Math.Min(framePyramid.Count, templatePyramid.Count);
        if (levels < 2)
        {
            return [];
        }

        var level = levels - 1;
        var coarseTemplate = templatePyramid[level];
        var coarseStatistics = TemplateStatistics.Create(coarseTemplate);
        if (!coarseStatistics.HasUsableVariance)
        {
            return [];
        }

        var candidates = FindCorrelationCandidates(
            framePyramid[level],
            validityFrame: null,
            validityOrigin: default,
            coarseTemplate,
            coarseStatistics,
            minimumSimilarity,
            AutomaticCandidateLimit,
            budget,
            cancellationToken);
        var positions = RefinePyramidPositions(
            framePyramid,
            templatePyramid,
            ApplyNonMaximumSuppression(candidates, coarseTemplate.Width, coarseTemplate.Height, AutomaticCandidateLimit),
            level,
            budget,
            cancellationToken);
        var accepted = new List<AutomaticCandidate>(capacity: 2);
        foreach (var position in positions)
        {
            var evidence = TryComputeAppearanceEvidence(
                frame,
                validityFrame,
                region,
                template,
                statistics,
                position.X,
                position.Y,
                cancellationToken);
            if (evidence is { } value
                && value.Correlation >= ((2.0 * minimumSimilarity) - 1.0)
                && value.Score >= minimumSimilarity)
            {
                AddAutomaticCandidate(
                    accepted,
                    new AutomaticCandidate(
                        position.X,
                        position.Y,
                        value.Score,
                        "luma-ncc",
                        EffectivePixels: statistics.EffectivePixelCount),
                    2);
            }
        }

        return accepted;
    }

    private static List<AutomaticCandidate> RefinePyramidPositions(
        IReadOnlyList<RgbImage> framePyramid,
        IReadOnlyList<RgbImage> templatePyramid,
        List<AutomaticCandidate> positions,
        int initialLevel,
        SearchBudget budget,
        CancellationToken cancellationToken)
    {
        for (var level = initialLevel - 1; level >= 0 && positions.Count > 0; level--)
        {
            var frame = framePyramid[level];
            var template = templatePyramid[level];
            var maximumSad = TemplateStatistics.Create(template).MaximumSad;
            var refined = new List<AutomaticCandidate>(capacity: AutomaticCandidateLimit * 4);
            foreach (var position in positions)
            {
                var centerX = checked(position.X * 2);
                var centerY = checked(position.Y * 2);
                var minX = Math.Max(0, centerX - PyramidRefinementRadius);
                var minY = Math.Max(0, centerY - PyramidRefinementRadius);
                var maxX = Math.Min(frame.Width - template.Width, centerX + PyramidRefinementRadius);
                var maxY = Math.Min(frame.Height - template.Height, centerY + PyramidRefinementRadius);
                for (var y = minY; y <= maxY; y++)
                {
                    for (var x = minX; x <= maxX; x++)
                    {
                        budget.Consume(EstimateAutomaticScanWork(1, template, correlation: false));
                        var sad = TryComputeWeightedSad(frame, validityFrame: null, validityOrigin: default, template, x, y, long.MaxValue, cancellationToken);
                        if (sad is { } value)
                        {
                            AddAutomaticCandidate(refined, new AutomaticCandidate(x, y, CalculateScore(value, maximumSad), "pyramid"), AutomaticCandidateLimit * 4);
                        }
                    }
                }
            }

            positions = ApplyNonMaximumSuppression(refined, template.Width, template.Height, AutomaticCandidateLimit);
        }

        return positions;
    }

    private static List<AutomaticCandidate> ScanWeightedCandidates(
        RgbImage frame,
        ScreenFrame? validityFrame,
        ScreenRect validityOrigin,
        RgbImage template,
        long allowedSad,
        double maximumSad,
        int limit,
        SearchBudget budget,
        CancellationToken cancellationToken)
    {
        var candidates = new List<AutomaticCandidate>(capacity: limit);
        var fullyValid = validityFrame is null || validityFrame.IsRectangleFullyValid(validityOrigin);
        var maxX = frame.Width - template.Width;
        var maxY = frame.Height - template.Height;
        for (var y = 0; y <= maxY; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var x = 0; x <= maxX; x++)
            {
                budget.Consume(EstimateAutomaticScanWork(1, template, correlation: false));
                long? sad;
                if (template.AlphaMask is null && fullyValid)
                {
                    // Normalize byte-SAD to coverage-weighted units.
                    var unweightedAllowedSad = allowedSad is long.MaxValue
                        ? long.MaxValue
                        : allowedSad / byte.MaxValue;
                    var contiguousSad = TryComputeContiguousSad(
                        frame,
                        template,
                        new ScreenRect(0, 0, frame.Width, frame.Height),
                        x,
                        y,
                        unweightedAllowedSad,
                        cancellationToken);
                    sad = contiguousSad is { } rawSad
                        ? checked(rawSad * byte.MaxValue)
                        : null;
                }
                else
                {
                    sad = TryComputeWeightedSad(frame, validityFrame, validityOrigin, template, x, y, allowedSad, cancellationToken);
                }

                if (sad is { } value)
                {
                    AddAutomaticCandidate(candidates, new AutomaticCandidate(x, y, CalculateScore(value, maximumSad), "weighted-sad"), limit);
                }
            }
        }

        return ApplyNonMaximumSuppression(candidates, template.Width, template.Height, limit);
    }

    private static List<AutomaticCandidate> FindCorrelationCandidates(
        RgbImage frame,
        ScreenFrame? validityFrame,
        ScreenRect validityOrigin,
        RgbImage template,
        TemplateStatistics statistics,
        double minimumSimilarity,
        int limit,
        SearchBudget budget,
        CancellationToken cancellationToken)
    {
        if (!statistics.HasUsableVariance || template.Width > frame.Width || template.Height > frame.Height)
        {
            return [];
        }

        var candidates = new List<AutomaticCandidate>(capacity: limit);
        var threshold = (2.0 * minimumSimilarity) - 1.0;
        var maxX = frame.Width - template.Width;
        var maxY = frame.Height - template.Height;
        for (var y = 0; y <= maxY; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var x = 0; x <= maxX; x++)
            {
                budget.Consume(EstimateAutomaticScanWork(1, template, correlation: true));
                var evidence = TryComputeAppearanceEvidence(
                    frame,
                    validityFrame,
                    validityOrigin,
                    template,
                    statistics,
                    x,
                    y,
                    cancellationToken);
                if (evidence is { } value
                    && value.Correlation >= threshold
                    && value.Score >= minimumSimilarity)
                {
                    AddAutomaticCandidate(
                        candidates,
                        new AutomaticCandidate(
                            x,
                            y,
                            value.Score,
                            "luma-ncc",
                            EffectivePixels: statistics.EffectivePixelCount),
                        limit);
                }
            }
        }

        return candidates;
    }

    private static long? TryComputeWeightedSad(
        RgbImage frame,
        ScreenFrame? validityFrame,
        ScreenRect validityOrigin,
        RgbImage template,
        int candidateX,
        int candidateY,
        long allowedSad,
        CancellationToken cancellationToken)
    {
        long sad = 0;
        for (var y = 0; y < template.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var x = 0; x < template.Width; x++)
            {
                var coverage = template.AlphaMask is null ? byte.MaxValue : template.AlphaMask[(y * template.Width) + x];
                if (coverage is 0)
                {
                    continue;
                }

                if (validityFrame is not null && !validityFrame.IsPixelValid(new ScreenPoint(validityOrigin.X + candidateX + x, validityOrigin.Y + candidateY + y)))
                {
                    return null;
                }

                var frameOffset = ((candidateY + y) * frame.RowStride) + ((candidateX + x) * ColorChannelCount);
                var templateOffset = (y * template.RowStride) + (x * ColorChannelCount);
                var difference = Math.Abs(frame.Pixels[frameOffset] - template.Pixels[templateOffset])
                    + Math.Abs(frame.Pixels[frameOffset + 1] - template.Pixels[templateOffset + 1])
                    + Math.Abs(frame.Pixels[frameOffset + 2] - template.Pixels[templateOffset + 2]);
                sad = checked(sad + (difference * (long)coverage));
                if (sad > allowedSad)
                {
                    return null;
                }
            }
        }

        return sad;
    }

    private static AppearanceEvidence? TryComputeAppearanceEvidence(
        RgbImage frame,
        ScreenFrame? validityFrame,
        ScreenRect validityOrigin,
        RgbImage template,
        TemplateStatistics statistics,
        int candidateX,
        int candidateY,
        CancellationToken cancellationToken)
    {
        if (statistics.EffectivePixelCount < MinimumAutomaticAppearanceEffectivePixels)
        {
            return null;
        }

        var correlation = TryComputeNormalizedCorrelation(
            frame,
            validityFrame,
            validityOrigin,
            template,
            statistics,
            candidateX,
            candidateY,
            cancellationToken);
        if (correlation is not { } measurement)
        {
            return null;
        }

        var photometricScore = TryComputePhotometricScore(
            frame,
            validityFrame,
            validityOrigin,
            template,
            statistics,
            measurement.FrameMean,
            candidateX,
            candidateY,
            cancellationToken);
        if (photometricScore is not { } score)
        {
            return null;
        }

        var correlationConfidence = Math.Clamp((measurement.Correlation + 1.0) / 2.0, 0.0, 1.0);
        var finalScore = Math.Min(correlationConfidence, score);
        return double.IsFinite(finalScore)
            ? new AppearanceEvidence(measurement.Correlation, finalScore)
            : null;
    }

    private static CorrelationMeasurement? TryComputeNormalizedCorrelation(
        RgbImage frame,
        ScreenFrame? validityFrame,
        ScreenRect validityOrigin,
        RgbImage template,
        TemplateStatistics statistics,
        int candidateX,
        int candidateY,
        CancellationToken cancellationToken)
    {
        double frameSum = 0;
        for (var y = 0; y < template.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var x = 0; x < template.Width; x++)
            {
                var coverage = template.AlphaMask is null ? byte.MaxValue : template.AlphaMask[(y * template.Width) + x];
                if (coverage is 0)
                {
                    continue;
                }

                if (validityFrame is not null && !validityFrame.IsPixelValid(new ScreenPoint(validityOrigin.X + candidateX + x, validityOrigin.Y + candidateY + y)))
                {
                    return null;
                }

                frameSum += coverage * GetLuma(frame, candidateX + x, candidateY + y);
            }
        }

        var frameMean = frameSum / statistics.CoverageSum;
        double numerator = 0;
        double frameVariance = 0;
        for (var y = 0; y < template.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var x = 0; x < template.Width; x++)
            {
                var coverage = template.AlphaMask is null ? byte.MaxValue : template.AlphaMask[(y * template.Width) + x];
                if (coverage is 0)
                {
                    continue;
                }

                var templateDeviation = GetLuma(template, x, y) - statistics.LumaMean;
                var frameDeviation = GetLuma(frame, candidateX + x, candidateY + y) - frameMean;
                numerator += coverage * templateDeviation * frameDeviation;
                frameVariance += coverage * frameDeviation * frameDeviation;
            }
        }

        // Restore coverage to the normalized template variance.
        var templateVariance = statistics.LumaVariance * statistics.CoverageSum;
        var denominator = Math.Sqrt(templateVariance * frameVariance);
        return denominator > 0.0 && double.IsFinite(denominator)
            ? new CorrelationMeasurement(Math.Clamp(numerator / denominator, -1.0, 1.0), frameMean)
            : null;
    }

    private static double? TryComputePhotometricScore(
        RgbImage frame,
        ScreenFrame? validityFrame,
        ScreenRect validityOrigin,
        RgbImage template,
        TemplateStatistics statistics,
        double frameMean,
        int candidateX,
        int candidateY,
        CancellationToken cancellationToken)
    {
        var offset = (int)Math.Round(
            Math.Clamp(frameMean - statistics.LumaMean, -MaximumAutomaticPhotometricOffset, MaximumAutomaticPhotometricOffset),
            MidpointRounding.AwayFromZero);
        long sad = 0;
        for (var y = 0; y < template.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var x = 0; x < template.Width; x++)
            {
                var coverage = template.AlphaMask is null ? byte.MaxValue : template.AlphaMask[(y * template.Width) + x];
                if (coverage is 0)
                {
                    continue;
                }

                if (validityFrame is not null
                    && !validityFrame.IsPixelValid(new ScreenPoint(validityOrigin.X + candidateX + x, validityOrigin.Y + candidateY + y)))
                {
                    return null;
                }

                var frameOffset = ((candidateY + y) * frame.RowStride) + ((candidateX + x) * ColorChannelCount);
                var templateOffset = (y * template.RowStride) + (x * ColorChannelCount);
                var red = Math.Clamp(frame.Pixels[frameOffset] - offset, 0, byte.MaxValue);
                var green = Math.Clamp(frame.Pixels[frameOffset + 1] - offset, 0, byte.MaxValue);
                var blue = Math.Clamp(frame.Pixels[frameOffset + 2] - offset, 0, byte.MaxValue);
                var difference = Math.Abs(red - template.Pixels[templateOffset])
                    + Math.Abs(green - template.Pixels[templateOffset + 1])
                    + Math.Abs(blue - template.Pixels[templateOffset + 2]);
                sad = checked(sad + (difference * (long)coverage));
            }
        }

        return CalculateScore(sad, statistics.MaximumSad);
    }

    private static List<RgbImage> BuildGaussianPyramid(
        RgbImage source,
        SearchBudget budget,
        CancellationToken cancellationToken,
        int maximumLevels = MaximumPyramidLevels)
    {
        var result = new List<RgbImage> { source };
        maximumLevels = Math.Clamp(maximumLevels, 1, MaximumPyramidLevels);
        while (result.Count < maximumLevels
            && result[^1].Width > MinimumPyramidTemplateExtent
            && result[^1].Height > MinimumPyramidTemplateExtent)
        {
            var current = result[^1];
            budget.ConsumePreparation(EstimateResamplingWork(current.Width, current.Height, sourcePixelCountPerOutput: 25));
            result.Add(GaussianDownsample(current, cancellationToken));
        }

        return result;
    }

    private sealed class AutomaticFramePyramidCache
    {
        private List<RgbImage>? _levels;

        public IReadOnlyList<RgbImage> Get(
            RgbImage source,
            int requiredLevels,
            SearchBudget budget,
            CancellationToken cancellationToken)
        {
            requiredLevels = Math.Clamp(requiredLevels, 1, MaximumPyramidLevels);
            _levels ??= [source];
            while (_levels.Count < requiredLevels
                && _levels[^1].Width > MinimumPyramidTemplateExtent
                && _levels[^1].Height > MinimumPyramidTemplateExtent)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var current = _levels[^1];
                budget.ConsumePreparation(EstimateResamplingWork(current.Width, current.Height, sourcePixelCountPerOutput: 25));
                _levels.Add(GaussianDownsample(current, cancellationToken));
            }

            return _levels;
        }
    }

    private IReadOnlyList<RgbImage> GetTemplatePyramid(PreparedTemplate preparedTemplate, SearchBudget budget, CancellationToken cancellationToken)
    {
        lock (_templateMaterializationLock)
        {
            lock (_templateCacheLock)
            {
                if (_templateCache.TryGetValue(preparedTemplate.Key, out var cached))
                {
                    _templateCacheLru.Remove(cached);
                    _templateCacheLru.AddFirst(cached);
                    if (cached.Value.Pyramid is { } pyramid)
                    {
                        return pyramid;
                    }
                }
            }

            var built = BuildGaussianPyramid(preparedTemplate.Image, budget, cancellationToken);
            _ = Interlocked.Increment(ref _templatePyramidBuildCount);
            var additionalBytes = built.Skip(1).Sum(static image => GetRgbImageByteCount(image));
            if (additionalBytes is 0)
            {
                return built;
            }

            lock (_templateCacheLock)
            {
                if (!_templateCache.TryGetValue(preparedTemplate.Key, out var cached))
                {
                    return built;
                }

                _templateCacheLru.Remove(cached);
                _templateCacheLru.AddFirst(cached);
                if (cached.Value.Pyramid is { } existing)
                {
                    return existing;
                }

                while (_templateCacheBytes + additionalBytes > _maxTemplateCacheBytes
                    && _templateCacheLru.Last is { } oldest
                    && !ReferenceEquals(oldest, cached))
                {
                    RemoveTemplateCacheEntry(oldest);
                }

                if (_templateCacheBytes + additionalBytes > _maxTemplateCacheBytes)
                {
                    return built;
                }

                cached.Value.Pyramid = built;
                cached.Value.SizeBytes = checked(cached.Value.SizeBytes + additionalBytes);
                _templateCacheBytes += additionalBytes;
                return built;
            }
        }
    }

    private static RgbImage GaussianDownsample(RgbImage source, CancellationToken cancellationToken)
    {
        ReadOnlySpan<int> kernel = [1, 4, 6, 4, 1];
        var width = Math.Max(1, (source.Width + 1) / 2);
        var height = Math.Max(1, (source.Height + 1) / 2);
        var pixels = new byte[checked(width * height * ColorChannelCount)];
        byte[]? coverage = source.AlphaMask is null ? null : new byte[checked(width * height)];
        for (var y = 0; y < height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var x = 0; x < width; x++)
            {
                long coverageTotal = 0;
                long totalWeight = 0;
                long red = 0;
                long green = 0;
                long blue = 0;
                for (var ky = -2; ky <= 2; ky++)
                {
                    var sourceY = Math.Clamp((y * 2) + ky, 0, source.Height - 1);
                    for (var kx = -2; kx <= 2; kx++)
                    {
                        var sourceX = Math.Clamp((x * 2) + kx, 0, source.Width - 1);
                        var filterWeight = kernel[ky + 2] * kernel[kx + 2];
                        var sourceCoverage = source.AlphaMask is null ? byte.MaxValue : source.AlphaMask[(sourceY * source.Width) + sourceX];
                        var effectiveWeight = filterWeight * (long)sourceCoverage;
                        var offset = (sourceY * source.RowStride) + (sourceX * ColorChannelCount);
                        totalWeight += filterWeight;
                        coverageTotal += effectiveWeight;
                        red += source.Pixels[offset] * effectiveWeight;
                        green += source.Pixels[offset + 1] * effectiveWeight;
                        blue += source.Pixels[offset + 2] * effectiveWeight;
                    }
                }

                var targetOffset = ((y * width) + x) * ColorChannelCount;
                if (coverageTotal is not 0)
                {
                    pixels[targetOffset] = (byte)((red + (coverageTotal / 2)) / coverageTotal);
                    pixels[targetOffset + 1] = (byte)((green + (coverageTotal / 2)) / coverageTotal);
                    pixels[targetOffset + 2] = (byte)((blue + (coverageTotal / 2)) / coverageTotal);
                }

                if (coverage is { })
                {
                    coverage[(y * width) + x] = (byte)((coverageTotal + (totalWeight / 2)) / totalWeight);
                }
            }
        }

        return CreateRgbImage(width, height, pixels, coverage);
    }

    private static List<AutomaticCandidate> ApplyNonMaximumSuppression(
        IEnumerable<AutomaticCandidate> candidates,
        int templateWidth,
        int templateHeight,
        int maximumCount)
    {
        var accepted = new List<AutomaticCandidate>(capacity: maximumCount);
        foreach (var candidate in candidates.OrderByDescending(static candidate => candidate.Score).ThenBy(static candidate => candidate.Y).ThenBy(static candidate => candidate.X))
        {
            var overlaps = accepted.Exists(existing => Math.Abs(existing.X - candidate.X) < Math.Max(1, templateWidth / 2)
                && Math.Abs(existing.Y - candidate.Y) < Math.Max(1, templateHeight / 2));
            if (!overlaps)
            {
                accepted.Add(candidate);
                if (accepted.Count == maximumCount)
                {
                    break;
                }
            }
        }

        return accepted;
    }

    private static void AddAutomaticCandidate(List<AutomaticCandidate> candidates, AutomaticCandidate candidate, int limit)
    {
        candidates.Add(candidate);
        candidates.Sort(AutomaticCandidateComparer.Instance);
        if (candidates.Count > limit)
        {
            candidates.RemoveRange(limit, candidates.Count - limit);
        }
    }

    private static double GetLuma(RgbImage image, int x, int y)
    {
        var offset = (y * image.RowStride) + (x * ColorChannelCount);
        return ((77.0 * image.Pixels[offset]) + (150.0 * image.Pixels[offset + 1]) + (29.0 * image.Pixels[offset + 2])) / 256.0;
    }

    private static long EstimateAutomaticScanWork(long candidateCount, RgbImage template, bool correlation)
    {
        var activePixels = template.AlphaMask is null
            ? checked((long)template.Width * template.Height)
            : template.AlphaMask.Count(static coverage => coverage is not 0);
        var channels = correlation ? 9L : ColorChannelCount;
        return SaturatingMultiply(SaturatingMultiply(candidateCount, activePixels), channels);
    }

    private PreparedTemplate GetScaledPreparedTemplate(
        ScreenFrame template,
        RgbImage source,
        TemplateCacheContent cacheContent,
        int width,
        int height,
        double scale,
        bool useAlphaMask,
        byte alphaThreshold,
        SearchBudget budget,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_templateMaterializationLock)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = CreateTemplateCacheKey(template, useAlphaMask, alphaThreshold, cacheContent, cancellationToken, (int)Math.Round(scale * 1000, MidpointRounding.AwayFromZero)) with { Width = width, Height = height };
            lock (_templateCacheLock)
            {
                if (_templateCache.TryGetValue(key, out var cached))
                {
                    _templateCacheLru.Remove(cached);
                    _templateCacheLru.AddFirst(cached);
                    return new PreparedTemplate(key, cached.Value.Image, cached.Value.Statistics);
                }
            }

            var useAreaResampling = width < source.Width || height < source.Height;
            budget.ConsumePreparation(EstimateResamplingWork(width, height, sourcePixelCountPerOutput: useAreaResampling ? 9 : 4));
            var scaled = useAreaResampling
                ? ResizeArea(source, width, height, cancellationToken)
                : ResizeLinear(source, width, height, cancellationToken);
            var statistics = TemplateStatistics.Create(scaled);
            var entryBytes = checked((long)key.Content.Bytes.Length + scaled.Pixels.LongLength + (scaled.AlphaMask?.LongLength ?? 0));
            if (entryBytes <= _maxTemplateCacheBytes)
            {
                lock (_templateCacheLock)
                {
                    if (_templateCache.TryGetValue(key, out var cached))
                    {
                        _templateCacheLru.Remove(cached);
                        _templateCacheLru.AddFirst(cached);
                        return new PreparedTemplate(key, cached.Value.Image, cached.Value.Statistics);
                    }

                    while (_templateCacheBytes + entryBytes > _maxTemplateCacheBytes && _templateCacheLru.Last is { } oldest)
                    {
                        RemoveTemplateCacheEntry(oldest);
                    }
                    var entry = new TemplateCacheEntry(key, scaled, statistics, entryBytes);
                    var node = _templateCacheLru.AddFirst(entry);
                    _templateCache.Add(key, node);
                    _templateCacheBytes += entryBytes;
                }
            }
            return new PreparedTemplate(key, scaled, statistics);
        }
    }

    private static RgbImage ResizeLinear(RgbImage source, int width, int height, CancellationToken cancellationToken)
    {
        var pixels = new byte[checked(width * height * ColorChannelCount)];
        byte[]? alphaMask = source.AlphaMask is null ? null : new byte[checked(width * height)];
        for (var y = 0; y < height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sourceYPosition = ((y + 0.5) * source.Height / height) - 0.5;
            var sourceYFloor = (int)Math.Floor(sourceYPosition);
            var sourceY = Math.Clamp(sourceYFloor, 0, source.Height - 1);
            var nextY = Math.Clamp(sourceYFloor + 1, 0, source.Height - 1);
            var yFraction = Math.Clamp(sourceYPosition - sourceYFloor, 0.0, 1.0);
            for (var x = 0; x < width; x++)
            {
                var sourceXPosition = ((x + 0.5) * source.Width / width) - 0.5;
                var sourceXFloor = (int)Math.Floor(sourceXPosition);
                var sourceX = Math.Clamp(sourceXFloor, 0, source.Width - 1);
                var nextX = Math.Clamp(sourceXFloor + 1, 0, source.Width - 1);
                var xFraction = Math.Clamp(sourceXPosition - sourceXFloor, 0.0, 1.0);
                var topLeft = checked(((sourceY * source.Width) + sourceX) * ColorChannelCount);
                var topRight = checked(((sourceY * source.Width) + nextX) * ColorChannelCount);
                var bottomLeft = checked(((nextY * source.Width) + sourceX) * ColorChannelCount);
                var bottomRight = checked(((nextY * source.Width) + nextX) * ColorChannelCount);
                var targetOffset = checked(((y * width) + x) * ColorChannelCount);
                var topLeftCoverage = GetCoverage(source, sourceX, sourceY);
                var topRightCoverage = GetCoverage(source, nextX, sourceY);
                var bottomLeftCoverage = GetCoverage(source, sourceX, nextY);
                var bottomRightCoverage = GetCoverage(source, nextX, nextY);
                var coverage = Interpolate(topLeftCoverage, topRightCoverage, bottomLeftCoverage, bottomRightCoverage, xFraction, yFraction);
                for (var channel = 0; channel < ColorChannelCount; channel++)
                {
                    var top = (source.Pixels[topLeft + channel] * topLeftCoverage)
                        + (((source.Pixels[topRight + channel] * topRightCoverage) - (source.Pixels[topLeft + channel] * topLeftCoverage)) * xFraction);
                    var bottom = (source.Pixels[bottomLeft + channel] * bottomLeftCoverage)
                        + (((source.Pixels[bottomRight + channel] * bottomRightCoverage) - (source.Pixels[bottomLeft + channel] * bottomLeftCoverage)) * xFraction);
                    var premultiplied = top + ((bottom - top) * yFraction);
                    pixels[targetOffset + channel] = ToStraightColor(premultiplied, coverage);
                }

                SetCoverage(alphaMask, width, x, y, coverage);
            }
        }

        return CreateRgbImage(width, height, pixels, alphaMask);
    }

    private static RgbImage ResizeArea(RgbImage source, int width, int height, CancellationToken cancellationToken)
    {
        var pixels = new byte[checked(width * height * ColorChannelCount)];
        byte[]? alphaMask = source.AlphaMask is null ? null : new byte[checked(width * height)];
        var pixelArea = source.Width / (double)width * (source.Height / (double)height);
        var premultiplied = new double[ColorChannelCount];
        for (var y = 0; y < height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sourceTop = y * source.Height / (double)height;
            var sourceBottom = (y + 1) * source.Height / (double)height;
            var firstSourceY = Math.Max(0, (int)Math.Floor(sourceTop));
            var lastSourceY = Math.Min(source.Height - 1, (int)Math.Ceiling(sourceBottom) - 1);
            for (var x = 0; x < width; x++)
            {
                var sourceLeft = x * source.Width / (double)width;
                var sourceRight = (x + 1) * source.Width / (double)width;
                var firstSourceX = Math.Max(0, (int)Math.Floor(sourceLeft));
                var lastSourceX = Math.Min(source.Width - 1, (int)Math.Ceiling(sourceRight) - 1);
                double coverageSum = 0;
                Array.Clear(premultiplied);
                for (var sourceY = firstSourceY; sourceY <= lastSourceY; sourceY++)
                {
                    var yOverlap = Math.Min(sourceBottom, sourceY + 1.0) - Math.Max(sourceTop, sourceY);
                    for (var sourceX = firstSourceX; sourceX <= lastSourceX; sourceX++)
                    {
                        var overlap = yOverlap * (Math.Min(sourceRight, sourceX + 1.0) - Math.Max(sourceLeft, sourceX));
                        if (overlap <= 0.0)
                        {
                            continue;
                        }

                        var sourceCoverage = GetCoverage(source, sourceX, sourceY);
                        var weightedCoverage = overlap * sourceCoverage;
                        coverageSum += weightedCoverage;
                        var sourceOffset = checked(((sourceY * source.Width) + sourceX) * ColorChannelCount);
                        for (var channel = 0; channel < ColorChannelCount; channel++)
                        {
                            premultiplied[channel] += source.Pixels[sourceOffset + channel] * weightedCoverage;
                        }
                    }
                }

                var coverage = coverageSum / pixelArea;
                var targetOffset = checked(((y * width) + x) * ColorChannelCount);
                for (var channel = 0; channel < ColorChannelCount; channel++)
                {
                    pixels[targetOffset + channel] = ToStraightColor(premultiplied[channel] / pixelArea, coverage);
                }

                SetCoverage(alphaMask, width, x, y, coverage);
            }
        }

        return CreateRgbImage(width, height, pixels, alphaMask);
    }

    private static double GetCoverage(RgbImage image, int x, int y) => image.AlphaMask is null
        ? byte.MaxValue
        : image.AlphaMask[(y * image.Width) + x];

    private static byte ToStraightColor(double premultiplied, double coverage) => coverage > 0.0
        ? (byte)Math.Clamp((int)Math.Round(premultiplied / coverage, MidpointRounding.AwayFromZero), 0, byte.MaxValue)
        : (byte)0;

    private static void SetCoverage(byte[]? alphaMask, int width, int x, int y, double coverage)
    {
        if (alphaMask is { })
        {
            alphaMask[(y * width) + x] = (byte)Math.Clamp(
                (int)Math.Round(coverage, MidpointRounding.AwayFromZero),
                0,
                byte.MaxValue);
        }
    }

    private static double Interpolate(double topLeft, double topRight, double bottomLeft, double bottomRight, double xFraction, double yFraction)
    {
        var top = topLeft + ((topRight - topLeft) * xFraction);
        var bottom = bottomLeft + ((bottomRight - bottomLeft) * xFraction);
        return top + ((bottom - top) * yFraction);
    }

    private static TemplateCacheKey CreateTemplateCacheKey(
        ScreenFrame template,
        bool useAlphaMask,
        byte alphaThreshold,
        TemplateCacheContent content,
        CancellationToken cancellationToken,
        int scaleKey = 0)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new TemplateCacheKey(
            template.Width,
            template.Height,
            template.PixelFormat,
            template.AlphaMode,
            useAlphaMask,
            alphaThreshold,
            scaleKey,
            content);
    }

    private static TemplateCacheContent CreateTemplateCacheContent(ScreenFrame template, SearchBudget budget, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var bytesPerPixel = ScreenFrame.GetBytesPerPixel(template.PixelFormat);
        var rowLength = checked(template.Width * bytesPerPixel);
        var rawLength = checked((long)rowLength * template.Height);
        var validityLength = !template.IsFullyValid
            ? checked((long)template.Width * template.Height)
            : 0;
        var contentLength = checked(rawLength + validityLength);
        budget.ConsumePreparation(contentLength);
        var content = new byte[checked((int)contentLength)];
        var source = template.Pixels.Span;
        for (var y = 0; y < template.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            source.Slice(checked(y * template.Stride), rowLength).CopyTo(content.AsSpan(y * rowLength, rowLength));
        }

        if (validityLength is not 0)
        {
            var validityOffset = checked((int)rawLength);
            for (var y = 0; y < template.Height; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (var x = 0; x < template.Width; x++)
                {
                    var point = new ScreenPoint(
                        checked(template.LogicalBounds.X + x),
                        checked(template.LogicalBounds.Y + y));
                    content[validityOffset + (y * template.Width) + x] = template.IsPixelValid(point)
                        ? (byte)1
                        : (byte)0;
                }
            }
        }

        return new TemplateCacheContent(content, ComputeContentHash(content));
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

    private static AnchorPoint[] BuildAnchorPoints(int width, int height, int requestedCount)
    {
        var pixelCount = checked(width * height);
        var anchorCount = Math.Min(requestedCount, pixelCount);
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
                : (int)Math.Round(anchorIndex * (pixelCount - 1) / (double)(anchorCount - 1), MidpointRounding.AwayFromZero);
            if (sampleIndex == previousIndex)
            {
                continue;
            }

            previousIndex = sampleIndex;
            anchors[uniqueCount++] = new AnchorPoint(sampleIndex % width, sampleIndex / width);
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
        long allowedSad,
        CancellationToken cancellationToken)
    {
        if (template.AlphaMask is null)
        {
            var unweightedAllowedSad = allowedSad is long.MaxValue
                ? long.MaxValue
                : allowedSad / byte.MaxValue;
            var contiguousSad = TryComputeContiguousSad(frame, template, frameBounds, candidateX, candidateY, unweightedAllowedSad, cancellationToken);
            return contiguousSad is { } value ? checked(value * byte.MaxValue) : null;
        }

        long sad = 0;
        for (var templateY = 0; templateY < template.Height; templateY++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var templateX = 0; templateX < template.Width; templateX++)
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

    private static long GetPixelSad(
        RgbImage frame,
        RgbImage template,
        ScreenRect frameBounds,
        int candidateX,
        int candidateY,
        int templateX,
        int templateY)
    {
        if (template.AlphaMask is not null && template.AlphaMask[(templateY * template.Width) + templateX] is 0)
        {
            return 0;
        }

        var frameLocalX = candidateX - frameBounds.X + templateX;
        var frameLocalY = candidateY - frameBounds.Y + templateY;
        var frameColor = ReadPixel(frame, frameLocalX, frameLocalY);
        var templateColor = ReadPixel(template, templateX, templateY);

        var difference = Math.Abs(frameColor.R - templateColor.R)
            + Math.Abs(frameColor.G - templateColor.G)
            + Math.Abs(frameColor.B - templateColor.B);
        var coverage = template.AlphaMask is null ? byte.MaxValue : template.AlphaMask[(templateY * template.Width) + templateX];
        return difference * (long)coverage;
    }

    private static RgbImage NormalizeFrame(ScreenFrame frame, bool useAlphaMask, byte alphaThreshold, CancellationToken cancellationToken)
    {
        var target = new byte[checked(frame.Width * frame.Height * ColorChannelCount)];
        var requiresAlphaMask = useAlphaMask
            && (frame.AlphaMode is ScreenAlphaMode.Straight or ScreenAlphaMode.Premultiplied);
        var requiresValidityMask = !frame.IsFullyValid;
        var alphaMask = requiresAlphaMask || requiresValidityMask
            ? new byte[checked(frame.Width * frame.Height)]
            : null;
        NormalizeFrameInto(frame, target, alphaMask, requiresAlphaMask, frame.LogicalBounds, alphaThreshold, cancellationToken);
        return CreateRgbImage(frame.Width, frame.Height, target, alphaMask);
    }

    private static RgbImage CreateRgbImage(int width, int height, byte[] pixels, byte[]? alphaMask)
    {
        var pixelCount = checked(width * height);
        var requiredPixelBytes = checked(pixelCount * ColorChannelCount);
        if (pixels.Length < requiredPixelBytes)
        {
            throw new ArgumentException("The normalized image buffer is smaller than the declared image dimensions.", nameof(pixels));
        }

        if (alphaMask is not null && alphaMask.Length < checked(width * height))
        {
            throw new ArgumentException("The alpha mask is smaller than the normalized image.", nameof(alphaMask));
        }

        var effectivePixelCount = alphaMask is null ? pixelCount : CountActivePixels(alphaMask);
        return new RgbImage(width, height, pixels, checked(width * ColorChannelCount), alphaMask, effectivePixelCount);
    }

    private static long GetRgbImageByteCount(RgbImage image) =>
        checked(image.Pixels.LongLength + (image.AlphaMask?.LongLength ?? 0));

    private static int CountEffectivePixels(RgbImage image)
    {
        if (image.AlphaMask is null)
        {
            return checked(image.Width * image.Height);
        }

        var count = 0;
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                if (image.AlphaMask[(y * image.Width) + x] is not 0)
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static int CountActivePixels(ReadOnlySpan<byte> alphaMask)
    {
        var count = 0;
        foreach (var value in alphaMask)
        {
            if (value is not 0)
            {
                count++;
            }
        }

        return count;
    }

    private static byte Unpremultiply(byte value, byte alpha) =>
        alpha is 0 ? (byte)0 : (byte)Math.Min(byte.MaxValue, ((value * 255) + (alpha / 2)) / alpha);

    private static RgbImage NormalizePooledFrame(ScreenFrame frame, ScreenRect region, CancellationToken cancellationToken)
    {
        var requiredLength = checked(region.Width * region.Height * ColorChannelCount);
        var target = ArrayPool<byte>.Shared.Rent(requiredLength);
        try
        {
            NormalizeFrameInto(frame, target, alphaMask: null, preserveAlphaCoverage: false, region, alphaThreshold: 1, cancellationToken);
            return new RgbImage(region.Width, region.Height, target, checked(region.Width * ColorChannelCount));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            ArrayPool<byte>.Shared.Return(target);
            throw;
        }
    }

    private static void NormalizeFrameInto(
        ScreenFrame frame,
        byte[] target,
        byte[]? alphaMask,
        bool preserveAlphaCoverage,
        ScreenRect region,
        byte alphaThreshold,
        CancellationToken cancellationToken)
    {
        var bytesPerPixel = ScreenFrame.GetBytesPerPixel(frame.PixelFormat);
        var source = frame.Pixels.Span;
        var rowLength = checked(region.Width * ColorChannelCount);
        var sourceOriginX = checked(region.X - frame.LogicalBounds.X);
        var sourceOriginY = checked(region.Y - frame.LogicalBounds.Y);

        if (!ShouldParallelizeRows(region.Width, region.Height)
            || !MemoryMarshal.TryGetArray(frame.Pixels, out var sourceSegment)
            || sourceSegment.Array is not { } sourceArray)
        {
            for (var y = 0; y < region.Height; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (var x = 0; x < region.Width; x++)
                {
                    var sourceOffset = checked(((sourceOriginY + y) * frame.Stride) + ((sourceOriginX + x) * bytesPerPixel));
                    var targetOffset = checked((y * rowLength) + (x * ColorChannelCount));
                    WriteNormalizedPixel(source, sourceOffset, frame.PixelFormat, frame.AlphaMode, target, targetOffset, alphaMask, preserveAlphaCoverage, (y * region.Width) + x, alphaThreshold);
                    ApplyValidityMask(frame, region, x, y, alphaMask);
                }
            }
        }
        else
        {
            void NormalizeRow(int y)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sourceRowOffset = checked(sourceSegment.Offset + ((sourceOriginY + y) * frame.Stride) + (sourceOriginX * bytesPerPixel));
                var targetRowOffset = checked(y * rowLength);
                for (var x = 0; x < region.Width; x++)
                {
                    var sourceOffset = checked(sourceRowOffset + (x * bytesPerPixel));
                    var targetOffset = checked(targetRowOffset + (x * ColorChannelCount));
                    WriteNormalizedPixel(sourceArray, sourceOffset, frame.PixelFormat, frame.AlphaMode, target, targetOffset, alphaMask, preserveAlphaCoverage, (y * region.Width) + x, alphaThreshold);
                    ApplyValidityMask(frame, region, x, y, alphaMask);
                }
            }

            _ = Parallel.For(
                0,
                region.Height,
                CreateParallelOptions(cancellationToken),
                NormalizeRow);
        }
    }

    private static void ApplyValidityMask(ScreenFrame frame, ScreenRect region, int localX, int localY, byte[]? alphaMask)
    {
        if (alphaMask is not null
            && !frame.IsFullyValid
            && !frame.IsPixelValid(new ScreenPoint(region.X + localX, region.Y + localY)))
        {
            alphaMask[(localY * region.Width) + localX] = 0;
        }
    }

    private static bool ShouldParallelizeRows(int width, int height) =>
        width >= MinimumParallelRowWidth
        && height >= MinimumParallelRowCount
        && (long)width * height >= ParallelPixelThreshold;

    private static ParallelOptions CreateParallelOptions(CancellationToken cancellationToken) => new()
    {
        CancellationToken = cancellationToken,
        MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, MaximumParallelism),
    };

    private static bool HasValidTemplateCoverage(ScreenFrame frame, RgbImage template, int candidateX, int candidateY)
    {
        for (var templateY = 0; templateY < template.Height; templateY++)
        {
            for (var templateX = 0; templateX < template.Width; templateX++)
            {
                if (template.AlphaMask is { } coverage && coverage[(templateY * template.Width) + templateX] is 0)
                {
                    continue;
                }

                if (!frame.IsPixelValid(new ScreenPoint(candidateX + templateX, candidateY + templateY)))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static void WriteNormalizedPixel(
        ReadOnlySpan<byte> source,
        int sourceOffset,
        ScreenPixelFormat pixelFormat,
        ScreenAlphaMode alphaMode,
        byte[] target,
        int targetOffset,
        byte[]? alphaMask,
        bool preserveAlphaCoverage,
        int alphaOffset,
        byte alphaThreshold)
    {
        byte red;
        byte green;
        byte blue;
        byte alpha = byte.MaxValue;
        switch (pixelFormat)
        {
            case ScreenPixelFormat.Rgb24:
            case ScreenPixelFormat.Xbgr8888:
            case ScreenPixelFormat.Abgr8888:
                red = source[sourceOffset];
                green = source[sourceOffset + 1];
                blue = source[sourceOffset + 2];
                if (pixelFormat is ScreenPixelFormat.Abgr8888)
                {
                    alpha = source[sourceOffset + 3];
                }
                break;
            case ScreenPixelFormat.Bgr24:
            case ScreenPixelFormat.Xrgb8888:
            case ScreenPixelFormat.Bgra8888:
                red = source[sourceOffset + 2];
                green = source[sourceOffset + 1];
                blue = source[sourceOffset];
                if (pixelFormat is ScreenPixelFormat.Bgra8888)
                {
                    alpha = source[sourceOffset + 3];
                }
                break;
            default:
                throw new InvalidOperationException($"Unsupported screen pixel format '{pixelFormat}'.");
        }

        if (alphaMode is ScreenAlphaMode.Premultiplied)
        {
            red = Unpremultiply(red, alpha);
            green = Unpremultiply(green, alpha);
            blue = Unpremultiply(blue, alpha);
        }

        target[targetOffset] = red;
        target[targetOffset + 1] = green;
        target[targetOffset + 2] = blue;
        if (alphaMask is { } mask)
        {
            if (!preserveAlphaCoverage)
            {
                mask[alphaOffset] = byte.MaxValue;
                return;
            }

            mask[alphaOffset] = alpha < alphaThreshold ? (byte)0 : alpha;
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

    private static long EstimateCandidateWork(RgbImage template, int anchorCount)
    {
        var pixelCount = SaturatingMultiply(template.Width, template.Height);
        return SaturatingMultiply(SaturatingAdd(pixelCount, Math.Max(0, anchorCount)), ColorChannelCount);
    }

    private static long EstimatePixelWork(int width, int height, bool includeAlpha = false)
    {
        var channels = includeAlpha ? ColorChannelCount + 1 : ColorChannelCount;
        return SaturatingMultiply(SaturatingMultiply(width, height), channels);
    }

    private static long EstimateResamplingWork(int width, int height, int sourcePixelCountPerOutput = 1)
    {
        var outputWork = EstimatePixelWork(width, height);
        return SaturatingMultiply(outputWork, sourcePixelCountPerOutput + 1L);
    }

    private static long SaturatingMultiply(long left, long right)
    {
        if (left <= 0 || right <= 0)
        {
            return 0;
        }

        return left > long.MaxValue / right ? long.MaxValue : left * right;
    }

    private static long SaturatingAdd(long left, long right)
    {
        if (left <= 0)
        {
            return Math.Max(0, right);
        }

        if (right <= 0)
        {
            return left;
        }

        return left > long.MaxValue - right ? long.MaxValue : left + right;
    }

    private static void RethrowResourceLimit(AggregateException exception)
    {
        var resourceLimit = exception.Flatten().InnerExceptions.OfType<ScreenImageMatcherResourceLimitException>().FirstOrDefault();
        if (resourceLimit is not null)
        {
            throw resourceLimit;
        }
    }

    private sealed class SearchBudget(long maximumWork, long maximumPreparationWork = MaxMatcherPreparationWork)
    {
        private long _consumedSearchWork;
        private long _consumedPreparationWork;

        public long ConsumedWork
        {
            get
            {
                var search = Volatile.Read(ref _consumedSearchWork);
                var preparation = Volatile.Read(ref _consumedPreparationWork);
                return preparation > long.MaxValue - search ? long.MaxValue : search + preparation;
            }
        }

        public long ConsumedSearchWork => Volatile.Read(ref _consumedSearchWork);

        public long ConsumedPreparationWork => Volatile.Read(ref _consumedPreparationWork);

        public bool CanConsume(long work)
        {
            if (work <= 0)
            {
                return true;
            }

            var consumed = Volatile.Read(ref _consumedSearchWork);
            return work <= maximumWork - consumed;
        }

        public void Consume(long work)
        {
            if (work <= 0)
            {
                return;
            }

            while (true)
            {
                var consumed = Volatile.Read(ref _consumedSearchWork);
                if (work > maximumWork - consumed)
                {
                    var requested = work > long.MaxValue - consumed ? long.MaxValue : consumed + work;
                    throw new ScreenImageMatcherResourceLimitException(
                        requested,
                        maximumWork,
                        $"Image matching exceeded the maximum work budget of {maximumWork.ToString("N0", CultureInfo.InvariantCulture)} channel comparisons.");
                }

                if (Interlocked.CompareExchange(ref _consumedSearchWork, consumed + work, consumed) == consumed)
                {
                    return;
                }
            }
        }

        public void ConsumePreparation(long work)
        {
            if (work <= 0)
            {
                return;
            }

            var consumed = Interlocked.Add(ref _consumedPreparationWork, work);
            if (consumed <= maximumPreparationWork)
            {
                return;
            }

            throw new ScreenImageMatcherResourceLimitException(
                consumed,
                maximumPreparationWork,
                $"Image matching preparation exceeded the maximum preparation budget of {maximumPreparationWork.ToString("N0", CultureInfo.InvariantCulture)} channel operations.")
            {
                IsPreparationLimit = true,
            };
        }
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private readonly record struct AnchorPoint(int X, int Y);

    private readonly record struct RgbImage(
        int Width,
        int Height,
        byte[] Pixels,
        int RowStride,
        byte[]? AlphaMask = null,
        int EffectivePixelCount = -1);

    private readonly record struct PreparedTemplate(
        TemplateCacheKey Key,
        RgbImage Image,
        TemplateStatistics Statistics);

    private readonly record struct TemplateCacheKey(
        int Width,
        int Height,
        ScreenPixelFormat PixelFormat,
        ScreenAlphaMode AlphaMode,
        bool UseAlphaMask,
        byte AlphaThreshold,
        int ScaleKey,
        TemplateCacheContent Content);

    private sealed class TemplateCacheContent(byte[] bytes, int contentHash)
    {
        public byte[] Bytes { get; } = bytes;

        public int ContentHash { get; } = contentHash;
    }

    private sealed class TemplateCacheKeyComparer : IEqualityComparer<TemplateCacheKey>
    {
        public static TemplateCacheKeyComparer Instance { get; } = new();

        public bool Equals(TemplateCacheKey left, TemplateCacheKey right)
        {
            return left.Width == right.Width
                && left.Height == right.Height
                && left.PixelFormat == right.PixelFormat
                && left.AlphaMode == right.AlphaMode
                && left.UseAlphaMask == right.UseAlphaMask
                && left.AlphaThreshold == right.AlphaThreshold
                && left.ScaleKey == right.ScaleKey
                && left.Content.ContentHash == right.Content.ContentHash
                && left.Content.Bytes.AsSpan().SequenceEqual(right.Content.Bytes);
        }

        public int GetHashCode(TemplateCacheKey key)
        {
            var hash = new HashCode();
            hash.Add(key.Width);
            hash.Add(key.Height);
            hash.Add(key.PixelFormat);
            hash.Add(key.AlphaMode);
            hash.Add(key.UseAlphaMask);
            hash.Add(key.AlphaThreshold);
            hash.Add(key.ScaleKey);
            hash.Add(key.Content.ContentHash);
            return hash.ToHashCode();
        }
    }

    private sealed class TemplateCacheEntry(
        ScreenImageMatcher.TemplateCacheKey key,
        ScreenImageMatcher.RgbImage image,
        ScreenImageMatcher.TemplateStatistics statistics,
        long sizeBytes)
    {
        public TemplateCacheKey Key { get; } = key;

        public RgbImage Image { get; } = image;

        public TemplateStatistics Statistics { get; } = statistics;

        public IReadOnlyList<RgbImage>? Pyramid { get; set; }

        public long SizeBytes { get; set; } = sizeBytes;
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

    private readonly record struct AutomaticCandidate(
        int X,
        int Y,
        double Score,
        string Profile,
        int Width = 0,
        int Height = 0,
        double Scale = 1.0,
        double Coverage = 1.0,
        double EffectivePixels = 0.0);

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private readonly record struct AppearanceEvidence(double Correlation, double Score);

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private readonly record struct CorrelationMeasurement(double Correlation, double FrameMean);

    private sealed class AutomaticCandidateComparer : IComparer<AutomaticCandidate>
    {
        public static AutomaticCandidateComparer Instance { get; } = new();

        public int Compare(AutomaticCandidate left, AutomaticCandidate right)
        {
            var score = right.Score.CompareTo(left.Score);
            if (score is not 0)
            {
                return score;
            }

            // Prefer RGB verification over equal correlation evidence.
            var profile = GetProfilePriority(left.Profile).CompareTo(GetProfilePriority(right.Profile));
            if (profile is not 0)
            {
                return profile;
            }

            // Prefer the least-rescaled result on equal scores.
            var scale = Math.Abs(left.Scale - 1.0).CompareTo(Math.Abs(right.Scale - 1.0));
            if (scale is not 0)
            {
                return scale;
            }

            var y = left.Y.CompareTo(right.Y);
            return y is not 0 ? y : left.X.CompareTo(right.X);
        }

        private static int GetProfilePriority(string profile) => profile switch
        {
            "weighted-sad" => 0,
            "luma-ncc" => 1,
            _ => 2,
        };
    }

    private readonly record struct AutomaticSearchResult(List<AutomaticCandidate> Candidates, bool CoarseEvidenceSufficient);

    private readonly record struct ScaleEvidence(double Scale, IReadOnlyList<AutomaticCandidate> Candidates)
    {
        public double BestScore => Candidates.Count is 0 ? double.NegativeInfinity : Candidates.Max(static candidate => candidate.Score);
    }

    // Keep automatic evidence internal.
    private readonly record struct MatchEvidence(
        AutomaticCandidate? Best,
        AutomaticCandidate? SecondBest,
        double Margin,
        double Coverage,
        double Scale,
        string? Profile)
    {
        public static MatchEvidence Create(IEnumerable<AutomaticCandidate> candidates)
        {
            var ordered = candidates.Order(AutomaticCandidateComparer.Instance).ToArray();
            var best = ordered.FirstOrDefault();
            if (ordered.Length is 0)
            {
                return new MatchEvidence(Best: null, SecondBest: null, Margin: 0.0, Coverage: 0.0, Scale: 1.0, Profile: null);
            }

            AutomaticCandidate? second = null;
            foreach (var candidate in ordered)
            {
                if (IsSpatiallyDistinct(best, candidate))
                {
                    second = candidate;
                    break;
                }
            }

            return new MatchEvidence(
                best,
                second,
                second is { } runnerUp ? Math.Max(0.0, best.Score - runnerUp.Score) : best.Score,
                best.Coverage,
                best.Scale,
                best.Profile);
        }

        public bool IsExactNativeMatch(double minimumSimilarity, bool requireDistinctEvidence) => Best is { } best
            && best.Profile is "weighted-sad"
            && Math.Abs(best.Scale - 1.0) < ScaleScoreTieTolerance
            && best.Score >= 1.0
            && IsAcceptable(minimumSimilarity, requireDistinctEvidence);

        public ScreenImageMatch? ToMatch(double minimumSimilarity, bool requireDistinctEvidence)
        {
            if (Best is not { } best || !IsAcceptable(minimumSimilarity, requireDistinctEvidence))
            {
                return null;
            }

            var isNativeScale = Math.Abs(best.Scale - 1.0) < ScaleScoreTieTolerance;
            return new ScreenImageMatch(
                new ScreenPoint(best.X, best.Y),
                best.Score,
                isNativeScale ? 0 : best.Width,
                isNativeScale ? 0 : best.Height);
        }

        private bool IsAcceptable(double minimumSimilarity, bool requireDistinctEvidence) => Best is { } best
            && best.Score >= minimumSimilarity
            && (best.Profile is not "luma-ncc" || best.EffectivePixels >= MinimumAutomaticAppearanceEffectivePixels)
            && (!requireDistinctEvidence || SecondBest is null || Margin >= AutomaticMinimumEvidenceMargin);

        private static bool IsSpatiallyDistinct(AutomaticCandidate best, AutomaticCandidate candidate)
        {
            if (candidate == best)
            {
                return false;
            }

            var bestCenterX = best.X + (best.Width / 2.0);
            var bestCenterY = best.Y + (best.Height / 2.0);
            var candidateCenterX = candidate.X + (candidate.Width / 2.0);
            var candidateCenterY = candidate.Y + (candidate.Height / 2.0);
            return Math.Abs(bestCenterX - candidateCenterX) > AutomaticSameTargetCenterTolerance
                || Math.Abs(bestCenterY - candidateCenterY) > AutomaticSameTargetCenterTolerance;
        }
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private readonly record struct TemplateStatistics(long CoverageSum, double LumaMean, double LumaVariance, double MaximumSad, double Coverage)
    {
        public bool HasUsableVariance => LumaVariance >= 16.0 && double.IsFinite(LumaVariance);

        public double EffectivePixelCount => CoverageSum / (double)byte.MaxValue;

        public static TemplateStatistics Create(RgbImage template)
        {
            long coverageSum = 0;
            double lumaSum = 0;
            for (var y = 0; y < template.Height; y++)
            {
                for (var x = 0; x < template.Width; x++)
                {
                    var coverage = template.AlphaMask is null ? byte.MaxValue : template.AlphaMask[(y * template.Width) + x];
                    if (coverage is 0)
                    {
                        continue;
                    }

                    coverageSum = SaturatingAdd(coverageSum, coverage);
                    lumaSum += coverage * GetLuma(template, x, y);
                }
            }

            if (coverageSum is 0)
            {
                return new TemplateStatistics(0, 0.0, 0.0, 0.0, 0.0);
            }

            var mean = lumaSum / coverageSum;
            double variance = 0;
            for (var y = 0; y < template.Height; y++)
            {
                for (var x = 0; x < template.Width; x++)
                {
                    var coverage = template.AlphaMask is null ? byte.MaxValue : template.AlphaMask[(y * template.Width) + x];
                    if (coverage is not 0)
                    {
                        var difference = GetLuma(template, x, y) - mean;
                        variance += coverage * difference * difference;
                    }
                }
            }

            return new TemplateStatistics(
                coverageSum,
                mean,
                variance / coverageSum,
                (double)coverageSum * ColorChannelCount * MaxChannelDifference,
                coverageSum / ((double)template.Width * template.Height * byte.MaxValue));
        }
    }
}
