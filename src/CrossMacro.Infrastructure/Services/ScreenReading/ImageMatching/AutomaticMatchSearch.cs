using System.Buffers;
using static CrossMacro.Infrastructure.Services.ScreenReading.ImageMatching.AutomaticMatchPolicy;
using static CrossMacro.Infrastructure.Services.ScreenReading.ImageMatching.MatcherWork;

namespace CrossMacro.Infrastructure.Services.ScreenReading.ImageMatching;

/// <summary>
/// One scale-tolerant search: tries the native scale, then coarse and locally refined scales, and reports a match
/// only when the collected evidence is strong and distinct. An instance holds the state of a single search.
/// </summary>
internal sealed class AutomaticMatchSearch(
    PreparedTemplateCache templates,
    ScreenFrame frame,
    ScreenFrame template,
    ScreenRect region,
    ScreenImageMatchOptions options,
    CancellationToken cancellationToken)
{
    private readonly SearchBudget _budget = new(MaxMatcherWork, MaxMatcherPreparationWork);
    private readonly FramePyramidCache _framePyramidCache = new();
    private readonly List<AutomaticCandidate> _allCandidates = new(capacity: 16);
    private readonly HashSet<(int Width, int Height)> _observedDimensions = [];
    private readonly bool _requireDistinctEvidence = options.SelectionMode is ScreenImageMatchSelectionMode.Automatic;
    private RgbImage _framePixels;

    internal ScreenImageMatch? Find()
    {
        _budget.ConsumePreparation(EstimatePixelWork(region.Width, region.Height));
        _framePixels = FrameNormalization.NormalizePooledFrame(frame, region, cancellationToken);
        try
        {
            var cacheContent = TemplateCacheContent.Create(template, _budget, cancellationToken);
            var nativeTemplate = templates.GetPrepared(template, options.UseTemplateAlphaMask, options.AlphaThreshold, _budget, cancellationToken, cacheContent);
            if (HasInsufficientAlphaEvidence(nativeTemplate))
            {
                return null;
            }

            try
            {
                return SearchScales(nativeTemplate, cacheContent);
            }
            catch (ScreenImageMatcherResourceLimitException exception) when (!exception.IsPreparationLimit)
            {
                return ToMatch(MatchEvidence.Create(_allCandidates));
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(_framePixels.Pixels);
        }
    }

    private ScreenImageMatch? SearchScales(PreparedTemplate nativeTemplate, TemplateCacheContent cacheContent)
    {
        EvaluateScale(nativeTemplate, scale: 1.0);
        _ = _observedDimensions.Add((nativeTemplate.Image.Width, nativeTemplate.Image.Height));
        var nativeEvidence = MatchEvidence.Create(_allCandidates);
        if (nativeEvidence.IsExactNativeMatch(options.MinimumSimilarity, _requireDistinctEvidence))
        {
            return ToMatch(nativeEvidence);
        }

        var scaleEvidence = new List<ScaleEvidence>();
        foreach (var scale in AutomaticCoarseScales)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var before = _allCandidates.Count;
            if (TryEvaluateNewScale(scale, nativeTemplate, cacheContent))
            {
                scaleEvidence.Add(new ScaleEvidence(scale, _allCandidates.Skip(before).ToArray()));
            }
        }

        foreach (var scale in scaleEvidence.OrderByDescending(static evidence => evidence.BestScore).ThenBy(static evidence => Math.Abs(evidence.Scale - 1.0)).Take(LocalRefinementCandidateCount).SelectMany(static evidence => LocalScaleOffsets.Select(offset => evidence.Scale * (1.0 + offset))))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (scale is < MinimumAutomaticScale or > MaximumAutomaticScale)
            {
                continue;
            }

            _ = TryEvaluateNewScale(scale, nativeTemplate, cacheContent);
        }

        return ToMatch(MatchEvidence.Create(_allCandidates));
    }

    private ScreenImageMatch? ToMatch(MatchEvidence evidence) => evidence.ToMatch(options.MinimumSimilarity, _requireDistinctEvidence);

    /// <summary>Evaluates the template at <paramref name="scale"/> unless its scaled size is unusable or was already tried.</summary>
    private bool TryEvaluateNewScale(double scale, PreparedTemplate nativeTemplate, TemplateCacheContent cacheContent)
    {
        var width = Math.Max(1, (int)Math.Round(template.Width * scale, MidpointRounding.AwayFromZero));
        var height = Math.Max(1, (int)Math.Round(template.Height * scale, MidpointRounding.AwayFromZero));
        if (width < MinimumAutomaticScaledTemplateExtent || height < MinimumAutomaticScaledTemplateExtent || width > region.Width || height > region.Height || !_observedDimensions.Add((width, height)))
        {
            return false;
        }

        var scaled = templates.GetScaled(template, nativeTemplate.Image, cacheContent, width, height, scale, options.UseTemplateAlphaMask, options.AlphaThreshold, _budget, cancellationToken);
        EvaluateScale(scaled, scale);
        return true;
    }

    private static bool HasInsufficientAlphaEvidence(PreparedTemplate preparedTemplate)
    {
        var image = preparedTemplate.Image;
        return image.AlphaMask is not null && checked((long)image.Width * image.Height) >= MinimumSparseTemplateArea && preparedTemplate.Statistics.EffectivePixelCount < MinimumAutomaticAppearanceEffectivePixels;
    }

    private void EvaluateScale(PreparedTemplate preparedTemplate, double scale)
    {
        var scaledImage = preparedTemplate.Image;
        var statistics = preparedTemplate.Statistics;
        if (statistics.CoverageSum is 0)
        {
            return;
        }

        var candidateCount = checked((long)(_framePixels.Width - scaledImage.Width + 1) * (_framePixels.Height - scaledImage.Height + 1));
        if (candidateCount <= 0)
        {
            return;
        }

        var result = FindScaleCandidates(preparedTemplate, candidateCount);
        foreach (var candidate in result.Candidates)
        {
            _allCandidates.Add(candidate with { X = checked(region.X + candidate.X), Y = checked(region.Y + candidate.Y), Scale = scale, Width = scaledImage.Width, Height = scaledImage.Height, Coverage = statistics.Coverage, EffectivePixels = statistics.EffectivePixelCount, });
        }
    }

    private AutomaticSearchResult FindScaleCandidates(PreparedTemplate preparedTemplate, long candidateCount)
    {
        var scaledImage = preparedTemplate.Image;
        var statistics = preparedTemplate.Statistics;
        var maximumSad = statistics.MaximumSad;
        var allowedSad = statistics.HasUsableVariance ? MatchScoring.CalculateAllowedSad(maximumSad, options.MinimumSimilarity) : 0;
        var fullScanWork = EstimateAutomaticScanWork(candidateCount, scaledImage, correlation: false);
        var isSmallSearch = fullScanWork <= AutomaticDirectScanWork;
        var result = isSmallSearch
            ? new AutomaticSearchResult(ScanWeighted(scaledImage, allowedSad, maximumSad), CoarseEvidenceSufficient: true)
            : FindPyramidCandidates(preparedTemplate, allowedSad, maximumSad);

        var correlationAttempted = false;
        if (statistics.HasUsableVariance && (result.Candidates.Count is 0 || (!isSmallSearch && !result.CoarseEvidenceSufficient)))
        {
            correlationAttempted = true;
            var correlationCandidates = isSmallSearch ? ScanCorrelation(scaledImage, statistics) : FindPyramidCorrelationCandidates(preparedTemplate, statistics);
            if (correlationCandidates.Count > 0)
            {
                result = result with
                {
                    Candidates = correlationCandidates,
                    CoarseEvidenceSufficient = true,
                };
            }
        }

        if (!isSmallSearch && !result.CoarseEvidenceSufficient && _budget.CanConsume(fullScanWork))
        {
            result = result with
            {
                Candidates = ScanWeighted(scaledImage, allowedSad, maximumSad),
                CoarseEvidenceSufficient = true,
            };
        }

        if (result.Candidates.Count is 0 && statistics.HasUsableVariance && !correlationAttempted)
        {
            result = result with
            {
                Candidates = ScanCorrelation(scaledImage, statistics),
            };
        }

        return result;
    }

    private List<AutomaticCandidate> ScanWeighted(RgbImage scaledImage, long allowedSad, double maximumSad)
    {
        return AutomaticScanner.ScanWeightedCandidates(_framePixels, frame, region, scaledImage, allowedSad, maximumSad, limit: AutomaticCandidateLimit, _budget, cancellationToken);
    }

    private List<AutomaticCandidate> ScanCorrelation(RgbImage scaledImage, TemplateStatistics statistics)
    {
        return AutomaticScanner.FindCorrelationCandidates(_framePixels, frame, region, scaledImage, statistics, options.MinimumSimilarity, limit: AutomaticCandidateLimit, _budget, cancellationToken);
    }

    private AutomaticSearchResult FindPyramidCandidates(PreparedTemplate preparedTemplate, long allowedSad, double maximumSad)
    {
        var scaledImage = preparedTemplate.Image;
        var templatePyramid = templates.GetPyramid(preparedTemplate, _budget, cancellationToken);
        var framePyramid = _framePyramidCache.Get(_framePixels, templatePyramid.Count, _budget, cancellationToken);
        var levels = Math.Min(framePyramid.Count, templatePyramid.Count);
        if (levels < 2)
        {
            return new AutomaticSearchResult([], CoarseEvidenceSufficient: false);
        }

        var level = levels - 1;
        var coarseFrame = framePyramid[level];
        var coarseTemplate = templatePyramid[level].Image;
        if (coarseTemplate.Width > coarseFrame.Width || coarseTemplate.Height > coarseFrame.Height)
        {
            return new AutomaticSearchResult([], CoarseEvidenceSufficient: false);
        }

        var coarseMaximumSad = templatePyramid[level].Statistics.MaximumSad;
        var coarse = AutomaticScanner.ScanWeightedCandidates(coarseFrame, validityFrame: null, validityOrigin: default, coarseTemplate, allowedSad: long.MaxValue, coarseMaximumSad, limit: AutomaticCandidateLimit, _budget, cancellationToken);
        var coarseEvidenceSufficient = coarse.Count > 0 && coarse[0].Score >= Math.Max(0.50, options.MinimumSimilarity - 0.25);
        var positions = AutomaticScanner.ApplyNonMaximumSuppression(coarse, coarseTemplate.Width, coarseTemplate.Height, AutomaticCandidateLimit);
        positions = AutomaticScanner.RefinePyramidPositions(framePyramid, templatePyramid, positions, level, _budget, cancellationToken);
        var accepted = new List<AutomaticCandidate>(capacity: 2);
        foreach (var position in positions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sad = AutomaticScanner.TryComputeWeightedSad(_framePixels, frame, region, scaledImage, position.X, position.Y, allowedSad, cancellationToken);
            if (sad is { } value)
            {
                AutomaticScanner.AddAutomaticCandidate(accepted, new AutomaticCandidate(position.X, position.Y, MatchScoring.CalculateScore(value, maximumSad), "weighted-sad"), limit: 2);
            }
        }

        return new AutomaticSearchResult(accepted, coarseEvidenceSufficient && accepted.Count > 0);
    }

    private List<AutomaticCandidate> FindPyramidCorrelationCandidates(PreparedTemplate preparedTemplate, TemplateStatistics statistics)
    {
        var scaledImage = preparedTemplate.Image;
        var templatePyramid = templates.GetPyramid(preparedTemplate, _budget, cancellationToken);
        var framePyramid = _framePyramidCache.Get(_framePixels, templatePyramid.Count, _budget, cancellationToken);
        var levels = Math.Min(framePyramid.Count, templatePyramid.Count);
        if (levels < 2)
        {
            return [];
        }

        var level = levels - 1;
        var coarseTemplate = templatePyramid[level].Image;
        var coarseStatistics = templatePyramid[level].Statistics;
        if (!coarseStatistics.HasUsableVariance)
        {
            return [];
        }

        var minimumSimilarity = options.MinimumSimilarity;
        var candidates = AutomaticScanner.FindCorrelationCandidates(framePyramid[level], validityFrame: null, validityOrigin: default, coarseTemplate, coarseStatistics, minimumSimilarity, AutomaticCandidateLimit, _budget, cancellationToken);
        var positions = AutomaticScanner.RefinePyramidPositions(framePyramid, templatePyramid, AutomaticScanner.ApplyNonMaximumSuppression(candidates, coarseTemplate.Width, coarseTemplate.Height, AutomaticCandidateLimit), level, _budget, cancellationToken);
        var accepted = new List<AutomaticCandidate>(capacity: 2);
        foreach (var position in positions)
        {
            var evidence = AutomaticScanner.TryComputeAppearanceEvidence(_framePixels, frame, region, scaledImage, statistics, position.X, position.Y, cancellationToken);
            if (evidence is { } value && value.Correlation >= ((2.0 * minimumSimilarity) - 1.0) && value.Score >= minimumSimilarity)
            {
                AutomaticScanner.AddAutomaticCandidate(accepted, new AutomaticCandidate(position.X, position.Y, value.Score, "luma-ncc", EffectivePixels: statistics.EffectivePixelCount), 2);
            }
        }

        return accepted;
    }

    private sealed class FramePyramidCache
    {
        private List<RgbImage>? _levels;

        public IReadOnlyList<RgbImage> Get(RgbImage source, int requiredLevels, SearchBudget budget, CancellationToken cancellationToken)
        {
            _levels ??= [source];
            ImageResampling.ExtendGaussianPyramid(_levels, requiredLevels, budget, cancellationToken);
            return _levels;
        }
    }
}
