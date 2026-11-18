using static CrossMacro.Infrastructure.Services.ScreenReading.ImageMatching.MatcherWork;

namespace CrossMacro.Infrastructure.Services.ScreenReading.ImageMatching;

/// <summary>Exact search at native scale: scans every candidate position in row bands and keeps the best or first hit.</summary>
internal sealed class DeterministicMatchSearch(PreparedTemplateCache templates)
{
    private const int MatcherRowBandHeight = 32;

    internal static bool IsWithinWorkBudget(ScreenRect region, ScreenFrame template, int anchorPointCount)
    {
        var candidateWidth = checked((long)region.Width - template.Width + 1);
        var candidateHeight = checked((long)region.Height - template.Height + 1);
        var candidateCount = SaturatingMultiply(candidateWidth, candidateHeight);
        var templatePixels = SaturatingMultiply(template.Width, template.Height);
        var anchors = Math.Min((long)Math.Max(0, anchorPointCount), templatePixels);
        var candidateWork = SaturatingMultiply(SaturatingAdd(templatePixels, anchors), ColorChannelCount);
        var totalWork = SaturatingMultiply(candidateCount, candidateWork);

        return totalWork <= MaxMatcherWork;
    }

    internal ScreenImageMatch? Find(ScreenFrame frame, ScreenFrame template, ScreenRect region, ScreenImageMatchOptions options, CancellationToken cancellationToken)
    {
        var anchors = BuildAnchorsWithinLimit(template, options.AnchorPointCount);
        var budget = new SearchBudget(MaxMatcherWork);
        budget.ConsumePreparation(EstimatePixelWork(region.Width, region.Height));
        var framePixels = FrameNormalization.NormalizePooledFrame(frame, region, cancellationToken);
        try
        {
            var preparedTemplate = templates.GetPrepared(template, options.UseTemplateAlphaMask, options.AlphaThreshold, budget, cancellationToken);
            var templatePixels = preparedTemplate.Image;
            var effectivePixelCount = FrameNormalization.CountEffectivePixels(templatePixels);
            if (effectivePixelCount is 0)
            {
                throw new ArgumentException("The template does not contain any pixels above its alpha threshold.", nameof(template));
            }

            var maximumSad = preparedTemplate.Statistics.MaximumSad;
            var allowedSad = MatchScoring.CalculateAllowedSad(maximumSad, options.MinimumSimilarity);
            var selectedCandidate = FindAcrossBands(framePixels, frame, templatePixels, region, anchors, allowedSad, options.SelectionMode, budget, cancellationToken);
            if (!selectedCandidate.HasValue)
            {
                return null;
            }

            return new ScreenImageMatch(new ScreenPoint(selectedCandidate.X, selectedCandidate.Y), MatchScoring.CalculateScore(selectedCandidate.Sad, maximumSad));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(framePixels.Pixels);
        }
    }

    private static AnchorPoint[] BuildAnchorsWithinLimit(ScreenFrame template, int anchorPointCount)
    {
        var sampleWidth = template.Width;
        var sampleHeight = template.Height;
        var samplePixelCount = SaturatingMultiply(sampleWidth, sampleHeight);
        if (samplePixelCount > MaxMatcherWork / ColorChannelCount)
        {
            var requestedWork = SaturatingMultiply(samplePixelCount, ColorChannelCount);
            throw new ScreenImageMatcherResourceLimitException(requestedWork, MaxMatcherWork, $"A single image matcher candidate requires more than {MaxMatcherWork.ToString("N0", CultureInfo.InvariantCulture)} channel comparisons, exceeding the internal limit.");
        }

        var requestedAnchorCount = Math.Min((long)anchorPointCount, samplePixelCount);
        if (SaturatingAdd(samplePixelCount, requestedAnchorCount) > MaxMatcherWork / ColorChannelCount)
        {
            var requestedWork = SaturatingMultiply(SaturatingAdd(samplePixelCount, requestedAnchorCount), ColorChannelCount);
            throw new ScreenImageMatcherResourceLimitException(requestedWork, MaxMatcherWork, $"A single image matcher candidate, including its requested prefilter, requires {requestedWork.ToString("N0", CultureInfo.InvariantCulture)} channel comparisons, exceeding the internal limit of {MaxMatcherWork.ToString("N0", CultureInfo.InvariantCulture)}.");
        }

        var anchors = SadScanner.BuildAnchorPoints(sampleWidth, sampleHeight, anchorPointCount);
        var singleCandidateWork = SaturatingMultiply(SaturatingAdd(samplePixelCount, anchors.LongLength), ColorChannelCount);
        if (singleCandidateWork > MaxMatcherWork)
        {
            throw new ScreenImageMatcherResourceLimitException(singleCandidateWork, MaxMatcherWork, $"A single image matcher candidate, including its prefilter, requires {singleCandidateWork.ToString("N0", CultureInfo.InvariantCulture)} channel comparisons, exceeding the internal limit of {MaxMatcherWork.ToString("N0", CultureInfo.InvariantCulture)}.");
        }

        return anchors;
    }

    private static MatchCandidate FindAcrossBands(RgbImage framePixels, ScreenFrame frame, RgbImage templatePixels, ScreenRect region, AnchorPoint[] anchors, long allowedSad, ScreenImageMatchSelectionMode selectionMode, SearchBudget budget, CancellationToken cancellationToken)
    {
        var candidateWidth = checked((long)region.Width - templatePixels.Width + 1);
        var candidateHeight = checked((long)region.Height - templatePixels.Height + 1);
        var selectedCandidate = MatchCandidate.None;
        for (long bandYOffset = 0; bandYOffset < candidateHeight; bandYOffset = checked(bandYOffset + MatcherRowBandHeight))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var bandHeight = Math.Min(MatcherRowBandHeight, candidateHeight - bandYOffset);
            var startY = checked((long)region.Y + bandYOffset);
            var endY = checked(startY + bandHeight);
            var bandCandidate = SadScanner.FindBestCandidate(framePixels, frame, templatePixels, region, region.X, checked((long)region.X + candidateWidth), startY, endY, anchors, allowedSad, selectionMode, budget, cancellationToken);
            if (selectionMode is ScreenImageMatchSelectionMode.FirstThresholdMatch)
            {
                if (bandCandidate.HasValue)
                {
                    return bandCandidate;
                }
            }
            else
            {
                selectedCandidate = MatchCandidate.BetterOf(selectedCandidate, bandCandidate);
                if (selectedCandidate.Sad is 0)
                {
                    break;
                }
            }
        }

        return selectedCandidate;
    }
}
