namespace CrossMacro.Infrastructure.Services.ScreenReading;
public sealed partial class ScreenImageMatcher : IDisposable
{
    private const int ColorChannelCount = 3;
    private const int MaxChannelDifference = byte.MaxValue;
    private const int CancellationCheckBlockBytes = 4096;
    private const long ParallelPixelThreshold = 256_000;
    private const int MinimumParallelRowWidth = 256;
    private const int MinimumParallelRowCount = 4;
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
    private readonly PreparedTemplateCache _preparedTemplates;
    private readonly Lock _lifetimeLock = new();
    // Task completion sources deliberately coordinate concurrent Dispose callers.
    // Unlike a disposable wait handle, their completion remains observable after
    // the owner has finished disposing, so a late concurrent caller cannot race
    // with resource cleanup.
    private readonly TaskCompletionSource _searchesCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _disposeCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);
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
    public ScreenImageMatcher() : this(MaxTemplateCacheBytes)
    {
    }

    internal ScreenImageMatcher(long maxTemplateCacheBytes)
    {
        if (maxTemplateCacheBytes is < 1 or > MaxTemplateCacheBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(maxTemplateCacheBytes), maxTemplateCacheBytes, $"Template cache size must be between 1 and {MaxTemplateCacheBytes} bytes.");
        }

        _preparedTemplates = new PreparedTemplateCache(maxTemplateCacheBytes);
    }

    internal int TemplateNormalizationCount => Volatile.Read(ref _templateNormalizationCount);
    // Test-only deterministic diagnostics.
    internal int TemplatePyramidBuildCount => Volatile.Read(ref _templatePyramidBuildCount);
    internal (long Work, int CandidateCount, long CandidateWork, long PreparationWork) LastAutomaticSearchDiagnostics => (Volatile.Read(ref _lastAutomaticSearchWork), Volatile.Read(ref _lastAutomaticCandidateCount), Volatile.Read(ref _lastAutomaticCandidateWork), Volatile.Read(ref _lastAutomaticPreparationWork));
    internal long LastDeterministicCandidateWork => Volatile.Read(ref _lastDeterministicCandidateWork);

    public ScreenImageMatch? FindMatch(ScreenFrame frame, ScreenFrame template, ScreenImageMatchOptions? options = null, CancellationToken cancellationToken = default)
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

        if (options.SelectionMode is not ScreenImageMatchSelectionMode.Automatic && (template.Width > region.Width || template.Height > region.Height))
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
        catch (ScreenImageMatcherResourceLimitException exception)when (!exception.IsPreparationLimit)
        {
            return FindAutomaticMatchWithPooledFrame(frame, template, region, options, cancellationToken);
        }
    }

    private static bool IsDeterministicSearchWithinWorkBudget(ScreenRect region, ScreenFrame template, int anchorPointCount, ScreenImageMatchSelectionMode selectionMode)
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

    private ScreenImageMatch? FindDeterministicMatch(ScreenFrame frame, ScreenFrame template, ScreenRect region, ScreenImageMatchOptions options, CancellationToken cancellationToken)
    {
        var sampleWidth = template.Width;
        var sampleHeight = template.Height;
        var samplePixelCount = SaturatingMultiply(sampleWidth, sampleHeight);
        if (samplePixelCount > MaxMatcherWork / ColorChannelCount)
        {
            var requestedWork = SaturatingMultiply(samplePixelCount, ColorChannelCount);
            throw new ScreenImageMatcherResourceLimitException(requestedWork, MaxMatcherWork, $"A single image matcher candidate requires more than {MaxMatcherWork.ToString("N0", CultureInfo.InvariantCulture)} channel comparisons, exceeding the internal limit.");
        }

        var requestedAnchorCount = Math.Min((long)options.AnchorPointCount, samplePixelCount);
        if (SaturatingAdd(samplePixelCount, requestedAnchorCount) > MaxMatcherWork / ColorChannelCount)
        {
            var requestedWork = SaturatingMultiply(SaturatingAdd(samplePixelCount, requestedAnchorCount), ColorChannelCount);
            throw new ScreenImageMatcherResourceLimitException(requestedWork, MaxMatcherWork, $"A single image matcher candidate, including its requested prefilter, requires {requestedWork.ToString("N0", CultureInfo.InvariantCulture)} channel comparisons, exceeding the internal limit of {MaxMatcherWork.ToString("N0", CultureInfo.InvariantCulture)}.");
        }

        var anchors = MatchAlgorithms.BuildAnchorPoints(sampleWidth, sampleHeight, options.AnchorPointCount);
        var singleCandidateWork = SaturatingMultiply(SaturatingAdd(samplePixelCount, anchors.LongLength), ColorChannelCount);
        if (singleCandidateWork > MaxMatcherWork)
        {
            throw new ScreenImageMatcherResourceLimitException(singleCandidateWork, MaxMatcherWork, $"A single image matcher candidate, including its prefilter, requires {singleCandidateWork.ToString("N0", CultureInfo.InvariantCulture)} channel comparisons, exceeding the internal limit of {MaxMatcherWork.ToString("N0", CultureInfo.InvariantCulture)}.");
        }

        var budget = new SearchBudget(MaxMatcherWork);
        budget.ConsumePreparation(EstimatePixelWork(region.Width, region.Height));
        var framePixels = ImagePreparation.NormalizePooledFrame(frame, region, cancellationToken);
        try
        {
            var preparedTemplate = GetPreparedTemplate(template, options.UseTemplateAlphaMask, options.AlphaThreshold, budget, cancellationToken);
            var templatePixels = preparedTemplate.Image;
            var effectivePixelCount = ImagePreparation.CountEffectivePixels(templatePixels);
            if (effectivePixelCount is 0)
            {
                throw new ArgumentException("The template does not contain any pixels above its alpha threshold.", nameof(template));
            }

            var maximumSad = preparedTemplate.Statistics.MaximumSad;
            var allowedSad = MatchAlgorithms.CalculateAllowedSad(maximumSad, options.MinimumSimilarity);
            var candidateWidth = checked((long)region.Width - template.Width + 1);
            var candidateHeight = checked((long)region.Height - template.Height + 1);
            var selectedCandidate = MatchCandidate.None;
            for (long bandYOffset = 0; bandYOffset < candidateHeight; bandYOffset = checked(bandYOffset + MatcherRowBandHeight))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var bandHeight = Math.Min(MatcherRowBandHeight, candidateHeight - bandYOffset);
                var startY = checked((long)region.Y + bandYOffset);
                var endY = checked(startY + bandHeight);
                var bandCandidate = MatchAlgorithms.FindBestCandidate(framePixels, frame, templatePixels, region, region.X, checked((long)region.X + candidateWidth), startY, endY, anchors, allowedSad, options.SelectionMode, budget, cancellationToken);
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

            return new ScreenImageMatch(new ScreenPoint(selectedCandidate.X, selectedCandidate.Y), MatchAlgorithms.CalculateScore(selectedCandidate.Sad, maximumSad));
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
            _disposeCompleted.Task.GetAwaiter().GetResult();
            return;
        }

        if (hasActiveSearches)
        {
            // Wait for in-flight searches to drain. ExitSearchLease will set
            // _searchesCompleted when the last lease is released.
            _searchesCompleted.Task.GetAwaiter().GetResult();
        }

        _preparedTemplates.Clear();
        lock (_lifetimeLock)
        {
            _disposed = true;
        }

        // Wake any waiting dispose callers. Completion sources are retained so
        // that callers which arrive after this method returns can observe the
        // terminal state without touching a disposed synchronization primitive.
        _ = _searchesCompleted.TrySetResult();
        _ = _disposeCompleted.TrySetResult();
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
            _ = _searchesCompleted.TrySetResult();
        }
    }

    private static void ValidateOptions(ScreenImageMatchOptions options)
    {
        if (!Enum.IsDefined(options.SelectionMode))
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.SelectionMode, "Image match selection mode is invalid.");
        }

        if (!double.IsFinite(options.MinimumSimilarity) || options.MinimumSimilarity is < 0.0 or > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.MinimumSimilarity, "Minimum similarity must be between 0.0 and 1.0.");
        }

        if (options.AnchorPointCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.AnchorPointCount, "Anchor point count cannot be negative.");
        }
    }

    private static void EnsureReadable(ScreenFrame frame)
    {
        _ = frame.TryGetPixel(new ScreenPoint(frame.LogicalBounds.X, frame.LogicalBounds.Y), out _);
    }

    private PreparedTemplate GetPreparedTemplate(ScreenFrame template, bool useAlphaMask, byte alphaThreshold, SearchBudget budget, CancellationToken cancellationToken, TemplateCacheContent? cacheContent = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var content = cacheContent ?? CreateTemplateCacheContent(template, budget, cancellationToken);
        var key = CreateTemplateCacheKey(template, useAlphaMask, alphaThreshold, content, cancellationToken);
        return _preparedTemplates.GetOrAdd(key, () => PrepareTemplate(template, key, useAlphaMask, alphaThreshold, budget, cancellationToken), cancellationToken);

    }

    private PreparedTemplate PrepareTemplate(ScreenFrame template, TemplateCacheKey key, bool useAlphaMask,
        byte alphaThreshold, SearchBudget budget, CancellationToken cancellationToken)
    {
        budget.ConsumePreparation(EstimatePixelWork(template.Width, template.Height, includeAlpha: useAlphaMask));
        var normalized = ImagePreparation.NormalizeFrame(template, useAlphaMask, alphaThreshold, cancellationToken);
        if (normalized.EffectivePixelCount is 0)
        {
            throw new ArgumentException("The template does not contain any pixels above its alpha threshold.", nameof(template));
        }

        var statistics = TemplateStatistics.Create(normalized);
        _ = Interlocked.Increment(ref _templateNormalizationCount);
        return new PreparedTemplate(key, normalized, statistics);
    }

    private ScreenImageMatch? FindAutomaticMatchWithPooledFrame(ScreenFrame frame, ScreenFrame template, ScreenRect region, ScreenImageMatchOptions options, CancellationToken cancellationToken)
    {
        var budget = new SearchBudget(MaxMatcherWork, MaxMatcherPreparationWork);
        budget.ConsumePreparation(EstimatePixelWork(region.Width, region.Height));
        var framePixels = ImagePreparation.NormalizePooledFrame(frame, region, cancellationToken);
        List<AutomaticCandidate>? allCandidates = null;
        var framePyramidCache = new AutomaticFramePyramidCache();
        try
        {
            var cacheContent = CreateTemplateCacheContent(template, budget, cancellationToken);
            var nativeTemplate = GetPreparedTemplate(template, options.UseTemplateAlphaMask, options.AlphaThreshold, budget, cancellationToken, cacheContent);
            if (HasInsufficientAutomaticAlphaEvidence(nativeTemplate))
            {
                return null;
            }

            allCandidates = new List<AutomaticCandidate>(capacity: 16);
            var observedDimensions = new HashSet<(int Width, int Height)>();
            var requireDistinctEvidence = options.SelectionMode is ScreenImageMatchSelectionMode.Automatic;
            try
            {
                EvaluateAutomaticScale(frame, framePixels, nativeTemplate, region, options, scale: 1.0, allCandidates, framePyramidCache, budget, cancellationToken);
                _ = observedDimensions.Add((nativeTemplate.Image.Width, nativeTemplate.Image.Height));
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
                    if (width < MinimumAutomaticScaledTemplateExtent || height < MinimumAutomaticScaledTemplateExtent || width > region.Width || height > region.Height || !observedDimensions.Add((width, height)))
                    {
                        continue;
                    }

                    var scaled = GetScaledPreparedTemplate(template, nativeTemplate.Image, cacheContent, width, height, scale, options.UseTemplateAlphaMask, options.AlphaThreshold, budget, cancellationToken);
                    var before = allCandidates.Count;
                    EvaluateAutomaticScale(frame, framePixels, scaled, region, options, scale, allCandidates, framePyramidCache, budget, cancellationToken);
                    scaleEvidence.Add(new ScaleEvidence(scale, allCandidates.Skip(before).ToArray()));
                }

                foreach (var scale in scaleEvidence.OrderByDescending(static evidence => evidence.BestScore).ThenBy(static evidence => Math.Abs(evidence.Scale - 1.0)).Take(2).SelectMany(static evidence => LocalScaleOffsets.Select(offset => evidence.Scale * (1.0 + offset))))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (scale is < 0.70 or > 1.50)
                    {
                        continue;
                    }

                    var width = Math.Max(1, (int)Math.Round(template.Width * scale, MidpointRounding.AwayFromZero));
                    var height = Math.Max(1, (int)Math.Round(template.Height * scale, MidpointRounding.AwayFromZero));
                    if (width < MinimumAutomaticScaledTemplateExtent || height < MinimumAutomaticScaledTemplateExtent || width > region.Width || height > region.Height || !observedDimensions.Add((width, height)))
                    {
                        continue;
                    }

                    var scaled = GetScaledPreparedTemplate(template, nativeTemplate.Image, cacheContent, width, height, scale, options.UseTemplateAlphaMask, options.AlphaThreshold, budget, cancellationToken);
                    EvaluateAutomaticScale(frame, framePixels, scaled, region, options, scale, allCandidates, framePyramidCache, budget, cancellationToken);
                }

                return MatchEvidence.Create(allCandidates).ToMatch(options.MinimumSimilarity, requireDistinctEvidence);
            }
            catch (ScreenImageMatcherResourceLimitException exception)when (!exception.IsPreparationLimit)
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

    private void EvaluateAutomaticScale(ScreenFrame validityFrame, RgbImage frame, PreparedTemplate preparedTemplate, ScreenRect region, ScreenImageMatchOptions options, double scale, List<AutomaticCandidate> destination, AutomaticFramePyramidCache framePyramidCache, SearchBudget budget, CancellationToken cancellationToken)
    {
        var template = preparedTemplate.Image;
        var statistics = preparedTemplate.Statistics;
        if (statistics.CoverageSum is 0)
        {
            return;
        }

        var maximumSad = statistics.MaximumSad;
        var allowedSad = statistics.HasUsableVariance ? MatchAlgorithms.CalculateAllowedSad(maximumSad, options.MinimumSimilarity) : 0;
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
            result = new AutomaticSearchResult(MatchAlgorithms.ScanWeightedCandidates(frame, validityFrame, region, template, allowedSad, maximumSad, limit: AutomaticCandidateLimit, budget, cancellationToken), CoarseEvidenceSufficient: true);
        }
        else
        {
            result = FindPyramidCandidates(frame, validityFrame, region, preparedTemplate, allowedSad, maximumSad, options.MinimumSimilarity, framePyramidCache, budget, cancellationToken);
        }

        var correlationAttempted = false;
        if (statistics.HasUsableVariance && (result.Candidates.Count is 0 || (!isSmallSearch && !result.CoarseEvidenceSufficient)))
        {
            correlationAttempted = true;
            var correlationCandidates = isSmallSearch ? MatchAlgorithms.FindCorrelationCandidates(frame, validityFrame, region, template, statistics, options.MinimumSimilarity, limit: AutomaticCandidateLimit, budget, cancellationToken) : FindPyramidCorrelationCandidates(frame, validityFrame, region, preparedTemplate, statistics, options.MinimumSimilarity, framePyramidCache, budget, cancellationToken);
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
                Candidates = MatchAlgorithms.ScanWeightedCandidates(frame, validityFrame, region, template, allowedSad, maximumSad, limit: AutomaticCandidateLimit, budget, cancellationToken),
                CoarseEvidenceSufficient = true,
            };
        }

        if (result.Candidates.Count is 0 && statistics.HasUsableVariance && !correlationAttempted)
        {
            var correlationCandidates = MatchAlgorithms.FindCorrelationCandidates(frame, validityFrame, region, template, statistics, options.MinimumSimilarity, limit: AutomaticCandidateLimit, budget, cancellationToken);
            result = result with
            {
                Candidates = correlationCandidates,
            };
        }

        foreach (var candidate in result.Candidates)
        {
            destination.Add(candidate with { X = checked(region.X + candidate.X), Y = checked(region.Y + candidate.Y), Scale = scale, Width = template.Width, Height = template.Height, Coverage = statistics.Coverage, EffectivePixels = statistics.EffectivePixelCount, });
        }
    }

    private static bool HasInsufficientAutomaticAlphaEvidence(PreparedTemplate preparedTemplate)
    {
        var template = preparedTemplate.Image;
        return template.AlphaMask is not null && checked((long)template.Width * template.Height) >= MinimumSparseTemplateArea && preparedTemplate.Statistics.EffectivePixelCount < MinimumAutomaticAppearanceEffectivePixels;
    }

    private AutomaticSearchResult FindPyramidCandidates(RgbImage frame, ScreenFrame validityFrame, ScreenRect region, PreparedTemplate preparedTemplate, long allowedSad, double maximumSad, double minimumSimilarity, AutomaticFramePyramidCache framePyramidCache, SearchBudget budget, CancellationToken cancellationToken)
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
        var coarse = MatchAlgorithms.ScanWeightedCandidates(coarseFrame, validityFrame: null, validityOrigin: default, coarseTemplate, allowedSad: long.MaxValue, coarseMaximumSad, limit: AutomaticCandidateLimit, budget, cancellationToken);
        var coarseEvidenceSufficient = coarse.Count > 0 && coarse[0].Score >= Math.Max(0.50, minimumSimilarity - 0.25);
        var positions = MatchAlgorithms.ApplyNonMaximumSuppression(coarse, coarseTemplate.Width, coarseTemplate.Height, AutomaticCandidateLimit);
        positions = MatchAlgorithms.RefinePyramidPositions(framePyramid, templatePyramid, positions, level, budget, cancellationToken);
        var accepted = new List<AutomaticCandidate>(capacity: 2);
        foreach (var position in positions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sad = MatchAlgorithms.TryComputeWeightedSad(frame, validityFrame, region, template, position.X, position.Y, allowedSad, cancellationToken);
            if (sad is { } value)
            {
                MatchAlgorithms.AddAutomaticCandidate(accepted, new AutomaticCandidate(position.X, position.Y, MatchAlgorithms.CalculateScore(value, maximumSad), "weighted-sad"), limit: 2);
            }
        }

        return new AutomaticSearchResult(accepted, coarseEvidenceSufficient && accepted.Count > 0);
    }

    private List<AutomaticCandidate> FindPyramidCorrelationCandidates(RgbImage frame, ScreenFrame validityFrame, ScreenRect region, PreparedTemplate preparedTemplate, TemplateStatistics statistics, double minimumSimilarity, AutomaticFramePyramidCache framePyramidCache, SearchBudget budget, CancellationToken cancellationToken)
    {
        var template = preparedTemplate.Image;
        var templatePyramid = GetTemplatePyramid(preparedTemplate, budget, cancellationToken);
        var framePyramid = framePyramidCache.Get(frame, templatePyramid.Count, budget, cancellationToken);
        var levels = Math.Min(framePyramid.Count, templatePyramid.Count);
        if (levels < 2)
        {
            return[];
        }

        var level = levels - 1;
        var coarseTemplate = templatePyramid[level];
        var coarseStatistics = TemplateStatistics.Create(coarseTemplate);
        if (!coarseStatistics.HasUsableVariance)
        {
            return[];
        }

        var candidates = MatchAlgorithms.FindCorrelationCandidates(framePyramid[level], validityFrame: null, validityOrigin: default, coarseTemplate, coarseStatistics, minimumSimilarity, AutomaticCandidateLimit, budget, cancellationToken);
        var positions = MatchAlgorithms.RefinePyramidPositions(framePyramid, templatePyramid, MatchAlgorithms.ApplyNonMaximumSuppression(candidates, coarseTemplate.Width, coarseTemplate.Height, AutomaticCandidateLimit), level, budget, cancellationToken);
        var accepted = new List<AutomaticCandidate>(capacity: 2);
        foreach (var position in positions)
        {
            var evidence = MatchAlgorithms.TryComputeAppearanceEvidence(frame, validityFrame, region, template, statistics, position.X, position.Y, cancellationToken);
            if (evidence is { } value && value.Correlation >= ((2.0 * minimumSimilarity) - 1.0) && value.Score >= minimumSimilarity)
            {
                MatchAlgorithms.AddAutomaticCandidate(accepted, new AutomaticCandidate(position.X, position.Y, value.Score, "luma-ncc", EffectivePixels: statistics.EffectivePixelCount), 2);
            }
        }

        return accepted;
    }

    private sealed class AutomaticFramePyramidCache
    {
        private List<RgbImage>? _levels;
        public IReadOnlyList<RgbImage> Get(RgbImage source, int requiredLevels, SearchBudget budget, CancellationToken cancellationToken)
        {
            requiredLevels = Math.Clamp(requiredLevels, 1, MaximumPyramidLevels);
            _levels ??= [source];
            while (_levels.Count < requiredLevels && _levels[^1].Width > MinimumPyramidTemplateExtent && _levels[^1].Height > MinimumPyramidTemplateExtent)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var current = _levels[^1];
                budget.ConsumePreparation(EstimateResamplingWork(current.Width, current.Height, sourcePixelCountPerOutput: 25));
                _levels.Add(ImagePreparation.GaussianDownsample(current, cancellationToken));
            }

            return _levels;
        }
    }

    private IReadOnlyList<RgbImage> GetTemplatePyramid(PreparedTemplate preparedTemplate, SearchBudget budget, CancellationToken cancellationToken)
    {
        return _preparedTemplates.GetPyramid(preparedTemplate, () =>
        {
            var pyramid = ImagePreparation.BuildGaussianPyramid(preparedTemplate.Image, budget, cancellationToken);
            _ = Interlocked.Increment(ref _templatePyramidBuildCount);
            return pyramid;
        }, cancellationToken);
    }

    private static long EstimateAutomaticScanWork(long candidateCount, RgbImage template, bool correlation)
    {
        var activePixels = template.AlphaMask is null ? checked((long)template.Width * template.Height) : template.AlphaMask.Count(static coverage => coverage is not 0);
        var channels = correlation ? 9L : ColorChannelCount;
        return SaturatingMultiply(SaturatingMultiply(candidateCount, activePixels), channels);
    }

    private PreparedTemplate GetScaledPreparedTemplate(ScreenFrame template, RgbImage source, TemplateCacheContent cacheContent, int width, int height, double scale, bool useAlphaMask, byte alphaThreshold, SearchBudget budget, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = CreateTemplateCacheKey(template, useAlphaMask, alphaThreshold, cacheContent, cancellationToken, (int)Math.Round(scale * 1000, MidpointRounding.AwayFromZero))with
        {
            Width = width,
            Height = height,
        };
        return _preparedTemplates.GetOrAdd(key, () =>
        {
            var useAreaResampling = width < source.Width || height < source.Height;
            budget.ConsumePreparation(EstimateResamplingWork(width, height, sourcePixelCountPerOutput: useAreaResampling ? 9 : 4));
            var scaled = useAreaResampling ? ImagePreparation.ResizeArea(source, width, height, cancellationToken) : ImagePreparation.ResizeLinear(source, width, height, cancellationToken);
            return new PreparedTemplate(key, scaled, TemplateStatistics.Create(scaled));
        }, cancellationToken);
    }

    private static TemplateCacheKey CreateTemplateCacheKey(ScreenFrame template, bool useAlphaMask, byte alphaThreshold, TemplateCacheContent content, CancellationToken cancellationToken, int scaleKey = 0)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new TemplateCacheKey(template.Width, template.Height, template.PixelFormat, template.AlphaMode, useAlphaMask, alphaThreshold, scaleKey, content);
    }

    private static TemplateCacheContent CreateTemplateCacheContent(ScreenFrame template, SearchBudget budget, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var bytesPerPixel = ScreenFrame.GetBytesPerPixel(template.PixelFormat);
        var rowLength = checked(template.Width * bytesPerPixel);
        var rawLength = checked((long)rowLength * template.Height);
        var validityLength = !template.IsFullyValid ? checked((long)template.Width * template.Height) : 0;
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
                    var point = new ScreenPoint(checked(template.LogicalBounds.X + x), checked(template.LogicalBounds.Y + y));
                    content[validityOffset + (y * template.Width) + x] = template.IsPixelValid(point) ? (byte)1 : (byte)0;
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
                    throw new ScreenImageMatcherResourceLimitException(requested, maximumWork, $"Image matching exceeded the maximum work budget of {maximumWork.ToString("N0", CultureInfo.InvariantCulture)} channel comparisons.");
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

            throw new ScreenImageMatcherResourceLimitException(consumed, maximumPreparationWork, $"Image matching preparation exceeded the maximum preparation budget of {maximumPreparationWork.ToString("N0", CultureInfo.InvariantCulture)} channel operations.")
            {
                IsPreparationLimit = true,
            };
        }
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private readonly record struct AnchorPoint(int X, int Y);
    private readonly record struct RgbImage(int Width, int Height, byte[] Pixels, int RowStride, byte[]? AlphaMask = null, int EffectivePixelCount = -1);
    private readonly record struct PreparedTemplate(TemplateCacheKey Key, RgbImage Image, TemplateStatistics Statistics);
    private readonly record struct TemplateCacheKey(int Width, int Height, ScreenPixelFormat PixelFormat, ScreenAlphaMode AlphaMode, bool UseAlphaMask, byte AlphaThreshold, int ScaleKey, TemplateCacheContent Content);
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
            return left.Width == right.Width && left.Height == right.Height && left.PixelFormat == right.PixelFormat && left.AlphaMode == right.AlphaMode && left.UseAlphaMask == right.UseAlphaMask && left.AlphaThreshold == right.AlphaThreshold && left.ScaleKey == right.ScaleKey && left.Content.ContentHash == right.Content.ContentHash && left.Content.Bytes.AsSpan().SequenceEqual(right.Content.Bytes);
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

    private sealed class TemplateCacheEntry(ScreenImageMatcher.TemplateCacheKey key, ScreenImageMatcher.RgbImage image, ScreenImageMatcher.TemplateStatistics statistics, long sizeBytes)
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

        if (!current.HasValue || candidate.Sad < current.Sad || (candidate.Sad == current.Sad && (candidate.Y < current.Y || (candidate.Y == current.Y && candidate.X < current.X))))
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

        if (!current.HasValue || candidate.Y < current.Y || (candidate.Y == current.Y && candidate.X < current.X))
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

    private readonly record struct AutomaticCandidate(int X, int Y, double Score, string Profile, int Width = 0, int Height = 0, double Scale = 1.0, double Coverage = 1.0, double EffectivePixels = 0.0);
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
    private readonly record struct MatchEvidence(AutomaticCandidate? Best, AutomaticCandidate? SecondBest, double Margin, double Coverage, double Scale, string? Profile)
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

            return new MatchEvidence(best, second, second is { } runnerUp ? Math.Max(0.0, best.Score - runnerUp.Score) : best.Score, best.Coverage, best.Scale, best.Profile);
        }

        public bool IsExactNativeMatch(double minimumSimilarity, bool requireDistinctEvidence) => Best is { } best && best.Profile is "weighted-sad" && Math.Abs(best.Scale - 1.0) < ScaleScoreTieTolerance && best.Score >= 1.0 && IsAcceptable(minimumSimilarity, requireDistinctEvidence);
        public ScreenImageMatch? ToMatch(double minimumSimilarity, bool requireDistinctEvidence)
        {
            if (Best is not { } best || !IsAcceptable(minimumSimilarity, requireDistinctEvidence))
            {
                return null;
            }

            var isNativeScale = Math.Abs(best.Scale - 1.0) < ScaleScoreTieTolerance;
            return new ScreenImageMatch(new ScreenPoint(best.X, best.Y), best.Score, isNativeScale ? 0 : best.Width, isNativeScale ? 0 : best.Height);
        }

        private bool IsAcceptable(double minimumSimilarity, bool requireDistinctEvidence) => Best is { } best && best.Score >= minimumSimilarity && (best.Profile is not "luma-ncc" || best.EffectivePixels >= MinimumAutomaticAppearanceEffectivePixels) && (!requireDistinctEvidence || SecondBest is null || Margin >= AutomaticMinimumEvidenceMargin);
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
            return Math.Abs(bestCenterX - candidateCenterX) > AutomaticSameTargetCenterTolerance || Math.Abs(bestCenterY - candidateCenterY) > AutomaticSameTargetCenterTolerance;
        }
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private readonly record struct TemplateStatistics(long CoverageSum, double LumaMean, double LumaVariance, double MaximumSad, double Coverage)
    {
        public bool HasUsableVariance => LumaVariance >= 16.0 && double.IsFinite(LumaVariance);
        public double EffectivePixelCount => CoverageSum / (double)byte.MaxValue;

        // SAD includes coverage for every template pixel, including fully opaque pixels.
        public static double GetOpaqueMaximumSad(int width, int height) =>
            GetMaximumSad((double)width * height * byte.MaxValue);

        private static double GetMaximumSad(double coverageSum) => coverageSum * ColorChannelCount * MaxChannelDifference;

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
                    lumaSum += coverage * MatchAlgorithms.GetLuma(template, x, y);
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
                        var difference = MatchAlgorithms.GetLuma(template, x, y) - mean;
                        variance += coverage * difference * difference;
                    }
                }
            }

            return new TemplateStatistics(coverageSum, mean, variance / coverageSum, GetMaximumSad(coverageSum), coverageSum / ((double)template.Width * template.Height * byte.MaxValue));
        }
    }
}
