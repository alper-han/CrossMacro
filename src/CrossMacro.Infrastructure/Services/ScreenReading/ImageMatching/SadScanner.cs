using System.Numerics;
using static CrossMacro.Infrastructure.Services.ScreenReading.ImageMatching.MatchScoring;
using static CrossMacro.Infrastructure.Services.ScreenReading.ImageMatching.MatcherWork;

namespace CrossMacro.Infrastructure.Services.ScreenReading.ImageMatching;

/// <summary>Exhaustive sum-of-absolute-differences scanning with an optional anchor-point prefilter.</summary>
internal static class SadScanner
{
    private const int CancellationCheckBlockBytes = 4096;

    internal static MatchCandidate FindBestCandidate(
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
                            ? MatchCandidate.EarlierOf(bestCandidate, rowBest)
                            : MatchCandidate.BetterOf(bestCandidate, rowBest);
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
        var earlySuccess = false;
        for (var candidateXValue = startX; candidateXValue < endX; candidateXValue++)
        {
            if ((candidateXValue - startX) % 32 == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
            if (earlySuccess)
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
                earlySuccess = true;
            }
            else
            {
                rowBest = MatchCandidate.BetterOf(rowBest, candidate);
            }
        }

        return rowBest;
    }

    internal static AnchorPoint[] BuildAnchorPoints(int width, int height, int requestedCount)
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

    internal static long? TryComputeContiguousSad(
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

    private static ScreenPixelColor ReadPixel(RgbImage image, int localX, int localY)
    {
        var offset = checked((localY * image.RowStride) + (localX * ColorChannelCount));
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

    private static void RethrowResourceLimit(AggregateException exception)
    {
        var resourceLimit = exception.Flatten().InnerExceptions.OfType<ScreenImageMatcherResourceLimitException>().FirstOrDefault();
        if (resourceLimit is not null)
        {
            throw resourceLimit;
        }
    }
}
