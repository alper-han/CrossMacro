using static CrossMacro.Infrastructure.Services.ScreenReading.ImageMatching.MatcherWork;
using static CrossMacro.Infrastructure.Services.ScreenReading.ImageMatching.AutomaticMatchPolicy;
using static CrossMacro.Infrastructure.Services.ScreenReading.ImageMatching.MatchScoring;

namespace CrossMacro.Infrastructure.Services.ScreenReading.ImageMatching;

/// <summary>Candidate scanning for automatic matching: coverage-weighted SAD, normalized correlation, non-maximum suppression and pyramid refinement.</summary>
internal static class AutomaticScanner
{
    internal static List<AutomaticCandidate> RefinePyramidPositions(
        IReadOnlyList<RgbImage> framePyramid,
        IReadOnlyList<TemplatePyramidLevel> templatePyramid,
        List<AutomaticCandidate> positions,
        int initialLevel,
        SearchBudget budget,
        CancellationToken cancellationToken)
    {
        for (var level = initialLevel - 1; level >= 0 && positions.Count > 0; level--)
        {
            var frame = framePyramid[level];
            var template = templatePyramid[level].Image;
            var maximumSad = templatePyramid[level].Statistics.MaximumSad;
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

    internal static List<AutomaticCandidate> ScanWeightedCandidates(
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
                    var contiguousSad = SadScanner.TryComputeContiguousSad(
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

    internal static List<AutomaticCandidate> FindCorrelationCandidates(
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

    internal static long? TryComputeWeightedSad(
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

    internal static AppearanceEvidence? TryComputeAppearanceEvidence(
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

    internal static List<AutomaticCandidate> ApplyNonMaximumSuppression(
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

    internal static void AddAutomaticCandidate(List<AutomaticCandidate> candidates, AutomaticCandidate candidate, int limit)
    {
        var index = candidates.BinarySearch(candidate, AutomaticCandidateComparer.Instance);
        if (index < 0)
        {
            index = ~index;
        }

        if (index >= limit)
        {
            return;
        }

        candidates.Insert(index, candidate);
        if (candidates.Count > limit)
        {
            candidates.RemoveAt(limit);
        }
    }
}
