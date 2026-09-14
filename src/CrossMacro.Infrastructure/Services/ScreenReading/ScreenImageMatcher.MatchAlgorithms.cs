namespace CrossMacro.Infrastructure.Services.ScreenReading;

public sealed partial class ScreenImageMatcher
{
    /// <summary>Stateless candidate scanning, scoring and refinement algorithms.</summary>
    private static class MatchAlgorithms
    {
        public static MatchCandidate FindBestCandidate(
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

        public static MatchCandidate FindBestCandidateStandard(
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

        public static MatchCandidate FindBestCandidateCoarseToFine(
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

            double targetSimilarity = 1.0 - (allowedSad / TemplateStatistics.GetOpaqueMaximumSad(template.Width, template.Height));
            double coarseSimilarity = Math.Max(0.5, targetSimilarity - 0.15); // Lower similarity threshold by 15% for decimation and phase shift margin
            double maximumSadDown = TemplateStatistics.GetOpaqueMaximumSad(templateDown.Width, templateDown.Height);
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

        public static RgbImage DownsampleBy2(RgbImage source, CancellationToken cancellationToken)
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

            return ImagePreparation.CreateRgbImage(w, h, pixels, alphaMask);
        }

        public static RgbImage CropAndDownsampleBy2(
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

            return ImagePreparation.CreateRgbImage(w, h, pixels, alphaMask);
        }

        public static MatchCandidate FindBestCandidateInRow(
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

        public static List<AutomaticCandidate> RefinePyramidPositions(
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

        public static List<AutomaticCandidate> ScanWeightedCandidates(
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

        public static List<AutomaticCandidate> FindCorrelationCandidates(
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

        public static long? TryComputeWeightedSad(
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

        public static AppearanceEvidence? TryComputeAppearanceEvidence(
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

        public static CorrelationMeasurement? TryComputeNormalizedCorrelation(
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

        public static double? TryComputePhotometricScore(
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



        public static List<AutomaticCandidate> ApplyNonMaximumSuppression(
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

        public static void AddAutomaticCandidate(List<AutomaticCandidate> candidates, AutomaticCandidate candidate, int limit)
        {
            candidates.Add(candidate);
            candidates.Sort(AutomaticCandidateComparer.Instance);
            if (candidates.Count > limit)
            {
                candidates.RemoveRange(limit, candidates.Count - limit);
            }
        }

        public static double GetLuma(RgbImage image, int x, int y)
        {
            var offset = (y * image.RowStride) + (x * ColorChannelCount);
            return ((77.0 * image.Pixels[offset]) + (150.0 * image.Pixels[offset + 1]) + (29.0 * image.Pixels[offset + 2])) / 256.0;
        }

        public static AnchorPoint[] BuildAnchorPoints(int width, int height, int requestedCount)
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

        public static bool PassesAnchorPrefilter(
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

        public static long? TryComputeSad(
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

        public static long? TryComputeContiguousSad(
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

        public static long GetPixelSad(
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











        public static byte Unpremultiply(byte value, byte alpha) =>
            alpha is 0 ? (byte)0 : (byte)Math.Min(byte.MaxValue, ((value * 255) + (alpha / 2)) / alpha);







        public static bool ShouldParallelizeRows(int width, int height) =>
            width >= MinimumParallelRowWidth
            && height >= MinimumParallelRowCount
            && (long)width * height >= ParallelPixelThreshold;

        public static ParallelOptions CreateParallelOptions(CancellationToken cancellationToken) => new()
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount),
        };

        public static bool HasValidTemplateCoverage(ScreenFrame frame, RgbImage template, int candidateX, int candidateY)
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



        public static ScreenPixelColor ReadPixel(RgbImage image, int localX, int localY)
        {
            var offset = checked(((localY * image.Width) + localX) * ColorChannelCount);
            return new ScreenPixelColor(image.Pixels[offset], image.Pixels[offset + 1], image.Pixels[offset + 2]);
        }

        public static long? TrySumAbsoluteDifferences(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right, long allowedSad, CancellationToken cancellationToken)
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

        public static long CalculateAllowedSad(double maximumSad, double minimumSimilarity)
        {
            return checked((long)Math.Floor(maximumSad * (1.0 - minimumSimilarity)));
        }

        public static uint SumByteVector(Vector<byte> vector)
        {
            Vector.Widen(vector, out Vector<ushort> lower, out Vector<ushort> upper);
            return SumUshortVector(lower) + SumUshortVector(upper);
        }

        public static uint SumUshortVector(Vector<ushort> vector)
        {
            Vector.Widen(vector, out Vector<uint> lower, out Vector<uint> upper);
            return SumUIntVector(lower) + SumUIntVector(upper);
        }

        public static uint SumUIntVector(Vector<uint> vector)
        {
            var sum = 0U;
            for (var index = 0; index < Vector<uint>.Count; index++)
            {
                sum += vector[index];
            }

            return sum;
        }

        public static double CalculateScore(long sad, double maximumSad)
        {
            if (maximumSad <= 0.0)
            {
                return 1.0;
            }

            return Math.Clamp(1.0 - (sad / maximumSad), 0.0, 1.0);
        }

    }
}
