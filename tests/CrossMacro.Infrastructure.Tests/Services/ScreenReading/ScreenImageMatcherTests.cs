
namespace CrossMacro.Infrastructure.Tests.Services.ScreenReading;


public sealed class ScreenImageMatcherTests : IDisposable
{
    private readonly ScreenImageMatcher _matcher = new();

    public void Dispose() => _matcher.Dispose();

    [Fact]
    public void ScreenImageMatchOptions_WhenSelectionModeIsOmitted_UsesAutomaticDefaults()
    {
        var options = new ScreenImageMatchOptions();

        Assert.Equal(ScreenImageMatchSelectionMode.Automatic, options.SelectionMode);
        Assert.Equal(ScreenImageMatchSelectionMode.Automatic, ScreenImageMatchOptions.Default.SelectionMode);
        Assert.Equal(0.95, options.MinimumSimilarity);
    }

    [Theory]
    [InlineData(ScreenImageMatchSelectionMode.FirstThresholdMatch)]
    [InlineData(ScreenImageMatchSelectionMode.BestMatch)]
    public void ScreenImageMatchOptions_Create_PreservesExplicitSelectionMode(ScreenImageMatchSelectionMode selectionMode)
    {
        var options = ScreenImageMatchOptions.Create(searchRegion: null, 1.0, selectionMode);

        Assert.Equal(selectionMode, options.SelectionMode);
    }

    [Fact]
    public void FindMatch_WhenSelectionModeIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        using var frame = CreateFrame(new ScreenRect(0, 0, 1, 1), ScreenPixelFormat.Rgb24, [[Black]]);
        using var template = CreateFrame(new ScreenRect(0, 0, 1, 1), ScreenPixelFormat.Rgb24, [[Black]]);
        var options = new ScreenImageMatchOptions
        {
            SelectionMode = (ScreenImageMatchSelectionMode)99,
        };

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => _matcher.FindMatch(frame, template, options, NonCancelableToken));

        Assert.Equal("options", exception.ParamName);
        Assert.Contains("selection mode", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FindMatch_WhenTemplateExistsExactly_ReturnsMatchPointAndPerfectScore()
    {
        using var frame = CreateFrame(
            new ScreenRect(0, 0, 4, 3),
            ScreenPixelFormat.Rgb24,
            [
                [Black, Black, Black, Black],
                [Black, Red, Green, Black],
                [Black, Blue, White, Black],
            ]);
        using var template = CreateFrame(
            new ScreenRect(0, 0, 2, 2),
            ScreenPixelFormat.Rgb24,
            [
                [Red, Green],
                [Blue, White],
            ]);

        var match = _matcher.FindMatch(frame, template, cancellationToken: NonCancelableToken);

        Assert.Equal(new ScreenImageMatch(new ScreenPoint(1, 1), 1.0), match);
    }

    [Fact]
    public void FindMatch_WhenMultipleCandidatesHaveSameScore_ReturnsFirstRowMajorMatchDeterministically()
    {
        using var frame = CreateFrame(
            new ScreenRect(0, 0, 5, 4),
            ScreenPixelFormat.Rgb24,
            [
                [Black, Black, Black, Black, Black],
                [Black, Red, Green, Black, Black],
                [Black, Blue, White, Red, Green],
                [Black, Black, Black, Blue, White],
            ]);
        using var template = CreateFrame(
            new ScreenRect(0, 0, 2, 2),
            ScreenPixelFormat.Rgb24,
            [
                [Red, Green],
                [Blue, White],
            ]);

        for (var attempt = 0; attempt < 10; attempt++)
        {
            var match = _matcher.FindMatch(frame, template, cancellationToken: NonCancelableToken);

            Assert.Equal(new ScreenImageMatch(new ScreenPoint(1, 1), 1.0), match);
        }
    }

    [Fact]
    public void FindMatch_FirstThresholdMatchStopsAtFirstAcceptedCandidate_BestMatchUsesLowestSad()
    {
        using var frame = CreateFrame(
            new ScreenRect(-7, -3, 4, 1),
            ScreenPixelFormat.Rgb24,
            [[Blue, Black, Red, Black]]);
        using var template = CreateFrame(new ScreenRect(0, 0, 1, 1), ScreenPixelFormat.Rgb24, [[Black]]);
        var firstOptions = new ScreenImageMatchOptions
        {
            MinimumSimilarity = 0.5,
            SelectionMode = ScreenImageMatchSelectionMode.FirstThresholdMatch,
        };
        var bestOptions = firstOptions with { SelectionMode = ScreenImageMatchSelectionMode.BestMatch };

        var expectedFirst = ScalarScreenImageMatcher.FindMatch(
            frame,
            template,
            firstOptions,
            ScalarMatchSelection.FirstThresholdMatch,
            NonCancelableToken);
        var expectedBest = ScalarScreenImageMatcher.FindMatch(
            frame,
            template,
            bestOptions,
            ScalarMatchSelection.BestMatch,
            NonCancelableToken);

        Assert.Equal(new ScreenPoint(-7, -3), expectedFirst?.Point);
        Assert.Equal(new ScreenPoint(-6, -3), expectedBest?.Point);
        Assert.Equal(expectedFirst, _matcher.FindMatch(frame, template, firstOptions, NonCancelableToken));
        Assert.Equal(expectedBest, _matcher.FindMatch(frame, template, bestOptions, NonCancelableToken));
    }

    [Fact]
    public void FindMatch_BothModesMatchScalarOracleAcrossEarlyMiddleAndLateBands()
    {
        using var template = CreateFrame(
            new ScreenRect(0, 0, 2, 2),
            ScreenPixelFormat.Rgb24,
            [[Red, Green], [Blue, White]]);

        foreach (var (x, y) in new[] { (1, 1), (19, 19), (38, 38) })
        {
            var pixels = Solid(40, 40, Black);
            pixels[y][x] = Red;
            pixels[y][x + 1] = Green;
            pixels[y + 1][x] = Blue;
            pixels[y + 1][x + 1] = White;
            using var frame = CreateFrame(new ScreenRect(-10, -20, 40, 40), ScreenPixelFormat.Rgb24, pixels);

            foreach (var selectionMode in Enum.GetValues<ScreenImageMatchSelectionMode>())
            {
                var options = new ScreenImageMatchOptions { SelectionMode = selectionMode };
                var selection = selectionMode is ScreenImageMatchSelectionMode.FirstThresholdMatch
                    ? ScalarMatchSelection.FirstThresholdMatch
                    : ScalarMatchSelection.BestMatch;
                var expected = ScalarScreenImageMatcher.FindMatch(frame, template, options, selection, NonCancelableToken);

                Assert.Equal(expected, _matcher.FindMatch(frame, template, options, NonCancelableToken));
                Assert.Equal(new ScreenPoint(x - 10, y - 20), expected?.Point);
            }
        }
    }

    [Theory]
    [InlineData(ScreenImageMatchSelectionMode.FirstThresholdMatch)]
    [InlineData(ScreenImageMatchSelectionMode.BestMatch)]
    public void FindMatch_WhenTemplateIsAbsent_ReturnsNull(ScreenImageMatchSelectionMode selectionMode)
    {
        using var frame = CreateFrame(new ScreenRect(0, 0, 2, 2), ScreenPixelFormat.Rgb24, Solid(2, 2, Black));
        using var template = CreateFrame(new ScreenRect(0, 0, 1, 1), ScreenPixelFormat.Rgb24, Solid(1, 1, Red));

        var match = _matcher.FindMatch(frame, template, new ScreenImageMatchOptions { SelectionMode = selectionMode }, NonCancelableToken);

        Assert.Null(match);
    }

    [Theory]
    [InlineData(ScreenImageMatchSelectionMode.FirstThresholdMatch)]
    [InlineData(ScreenImageMatchSelectionMode.BestMatch)]
    public void FindMatch_SkipsInvalidMaskedPixelsThatMatchTemplate(ScreenImageMatchSelectionMode selectionMode)
    {
        using var frame = CreateFrame(
            new ScreenRect(0, 0, 2, 1),
            ScreenPixelFormat.Rgb24,
            [[Black, Black]],
            validPixelMask: [0, 1]);
        using var template = CreateFrame(new ScreenRect(0, 0, 1, 1), ScreenPixelFormat.Rgb24, [[Black]]);

        var match = _matcher.FindMatch(frame, template, new ScreenImageMatchOptions { SelectionMode = selectionMode }, NonCancelableToken);

        Assert.Equal(new ScreenImageMatch(new ScreenPoint(1, 0), 1.0), match);
    }

    [Fact]
    public void FindMatch_UsesStraightAlphaAsTemplateMask()
    {
        using var frame = CreateFrame(
            new ScreenRect(0, 0, 4, 1),
            ScreenPixelFormat.Rgb24,
            [[Black, Blue, Green, Black]]);
        using var template = new ScreenFrame(
            new ScreenRect(0, 0, 2, 1),
            stride: 8,
            ScreenPixelFormat.Abgr8888,
            new byte[]
            {
                0xFF, 0x00, 0x00, 0x00,
                0x00, 0xFF, 0x00, 0xFF,
            },
            alphaMode: ScreenAlphaMode.Straight);

        var match = _matcher.FindMatch(frame, template);

        Assert.Equal(new ScreenImageMatch(new ScreenPoint(1, 0), 1.0), match);
    }

    [Fact]
    public void FindMatch_AutomaticUsesPartialAlphaAsCoverageWeight()
    {
        using var frame = CreateFrame(
            new ScreenRect(0, 0, 2, 1),
            ScreenPixelFormat.Rgb24,
            [[Black, Green]]);
        using var template = new ScreenFrame(
            new ScreenRect(0, 0, 2, 1),
            stride: 8,
            ScreenPixelFormat.Abgr8888,
            new byte[]
            {
                Red.R, Red.G, Red.B, 0x80,
                Green.R, Green.G, Green.B, 0xFF,
            },
            alphaMode: ScreenAlphaMode.Straight);

        var match = _matcher.FindMatch(frame, template, new ScreenImageMatchOptions
        {
            SelectionMode = ScreenImageMatchSelectionMode.Automatic,
            MinimumSimilarity = 0.86,
        }, NonCancelableToken);

        Assert.Equal(new ScreenPoint(0, 0), match?.Point);
        Assert.InRange(match?.Score ?? 0.0, 0.88, 0.90);
    }

    [Theory]
    [InlineData(ScreenImageMatchSelectionMode.FirstThresholdMatch)]
    [InlineData(ScreenImageMatchSelectionMode.BestMatch)]
    public void FindMatch_DeterministicModesUsePartialAlphaAsCoverageWeight(ScreenImageMatchSelectionMode selectionMode)
    {
        using var frame = CreateFrame(
            new ScreenRect(0, 0, 2, 1),
            ScreenPixelFormat.Rgb24,
            [[Black, Green]]);
        using var template = new ScreenFrame(
            new ScreenRect(0, 0, 2, 1),
            stride: 8,
            ScreenPixelFormat.Abgr8888,
            new byte[]
            {
                Red.R, Red.G, Red.B, 0x80,
                Green.R, Green.G, Green.B, 0xFF,
            },
            alphaMode: ScreenAlphaMode.Straight);

        var match = _matcher.FindMatch(template: template, frame: frame, options: new ScreenImageMatchOptions
        {
            SelectionMode = selectionMode,
            MinimumSimilarity = 0.86,
        }, cancellationToken: NonCancelableToken);

        Assert.Equal(new ScreenPoint(0, 0), match?.Point);
        Assert.InRange(match?.Score ?? 0.0, 0.88, 0.90);
    }

    [Fact]
    public void FindMatch_RejectsFullyTransparentTemplate()
    {
        using var frame = CreateFrame(new ScreenRect(0, 0, 2, 1), ScreenPixelFormat.Rgb24, [[Black, Black]]);
        using var template = new ScreenFrame(
            new ScreenRect(0, 0, 1, 1),
            stride: 4,
            ScreenPixelFormat.Abgr8888,
            new byte[] { 0xFF, 0x00, 0x00, 0x00 },
            alphaMode: ScreenAlphaMode.Straight);

        Assert.Throws<ArgumentException>(() => _matcher.FindMatch(frame, template));
    }

    [Theory]
    [InlineData(ScreenImageMatchSelectionMode.FirstThresholdMatch)]
    [InlineData(ScreenImageMatchSelectionMode.BestMatch)]
    public void FindMatch_RequiresCompleteTemplateAreaToBeValid(ScreenImageMatchSelectionMode selectionMode)
    {
        using var frame = CreateFrame(
            new ScreenRect(0, 0, 3, 1),
            ScreenPixelFormat.Rgb24,
            [[Black, Black, Black]],
            validPixelMask: [1, 0, 1]);
        using var template = CreateFrame(new ScreenRect(0, 0, 2, 1), ScreenPixelFormat.Rgb24, [[Black, Black]]);

        var match = _matcher.FindMatch(frame, template, new ScreenImageMatchOptions { SelectionMode = selectionMode }, NonCancelableToken);

        Assert.Null(match);
    }

    [Fact]
    public void FindMatch_WhenSimilarityIsBelowThreshold_ReturnsNull()
    {
        using var frame = CreateFrame(
            new ScreenRect(0, 0, 2, 2),
            ScreenPixelFormat.Rgb24,
            [
                [Black, Black],
                [Black, Blue],
            ]);
        using var template = CreateFrame(new ScreenRect(0, 0, 2, 2), ScreenPixelFormat.Rgb24, Solid(2, 2, Black));

        var match = _matcher.FindMatch(frame, template, new ScreenImageMatchOptions { MinimumSimilarity = 0.95 }, NonCancelableToken);

        Assert.Null(match);
    }

    [Fact]
    public void FindMatch_WhenSimilarityMeetsThreshold_ReturnsScoreFromNormalizedSad()
    {
        using var frame = CreateFrame(
            new ScreenRect(0, 0, 2, 2),
            ScreenPixelFormat.Rgb24,
            [
                [Black, Black],
                [Black, Blue],
            ]);
        using var template = CreateFrame(new ScreenRect(0, 0, 2, 2), ScreenPixelFormat.Rgb24, Solid(2, 2, Black));

        var match = _matcher.FindMatch(frame, template, new ScreenImageMatchOptions { MinimumSimilarity = 0.9, SelectionMode = ScreenImageMatchSelectionMode.FirstThresholdMatch }, NonCancelableToken);

        _ = Assert.NotNull(match);
        Assert.Equal(new ScreenPoint(0, 0), match.Value.Point);
        Assert.Equal(1.0 - (255.0 / (2 * 2 * 3 * 255.0)), match.Value.Score, precision: 12);
    }

    [Fact]
    public void FindMatch_WhenSadEqualsIntegerThreshold_IncludesCandidate()
    {
        using var frame = CreateFrame(new ScreenRect(0, 0, 1, 1), ScreenPixelFormat.Rgb24, [[Blue]]);
        using var template = CreateFrame(new ScreenRect(0, 0, 1, 1), ScreenPixelFormat.Rgb24, [[Black]]);

        var match = _matcher.FindMatch(frame, template, new ScreenImageMatchOptions { MinimumSimilarity = 0.0, SelectionMode = ScreenImageMatchSelectionMode.FirstThresholdMatch }, NonCancelableToken);

        Assert.Equal(new ScreenImageMatch(new ScreenPoint(0, 0), 1.0 - (255.0 / 765.0)), match);
    }

    [Fact]
    public void FindMatch_BestMatchKeepsEqualSadCandidateForRowMajorTieBreak()
    {
        using var frame = CreateFrame(
            new ScreenRect(0, 0, 3, 1),
            ScreenPixelFormat.Rgb24,
            [[Red, Black, Red]]);
        using var template = CreateFrame(new ScreenRect(0, 0, 1, 1), ScreenPixelFormat.Rgb24, [[Black]]);

        var match = _matcher.FindMatch(frame, template, new ScreenImageMatchOptions
        {
            MinimumSimilarity = 0.5,
            SelectionMode = ScreenImageMatchSelectionMode.BestMatch,
        }, NonCancelableToken);

        Assert.Equal(new ScreenImageMatch(new ScreenPoint(1, 0), 1.0), match);
    }

    [Fact]
    public void FindMatch_BestMatchStopsAfterFirstCompletedBandWithPerfectMatch()
    {
        const int frameSize = 64;
        const int firstBandHeight = 32;
        using var frame = CreateSolidFrame(new ScreenRect(0, 0, frameSize, frameSize), Black);
        using var template = CreateFrame(new ScreenRect(0, 0, 1, 1), ScreenPixelFormat.Rgb24, [[Black]]);

        var match = _matcher.FindMatch(frame, template, new ScreenImageMatchOptions
        {
            MinimumSimilarity = 1.0,
            SelectionMode = ScreenImageMatchSelectionMode.BestMatch,
        }, NonCancelableToken);

        Assert.Equal(new ScreenImageMatch(new ScreenPoint(0, 0), 1.0), match);
        Assert.Equal((long)frameSize * firstBandHeight * 6, _matcher.LastDeterministicCandidateWork);
    }

    [Theory]
    [InlineData(ScreenImageMatchSelectionMode.FirstThresholdMatch)]
    [InlineData(ScreenImageMatchSelectionMode.BestMatch)]
    public void FindMatch_WhenSimilarityIsZero_AllowsTheBoundaryValue(ScreenImageMatchSelectionMode selectionMode)
    {
        using var frame = CreateFrame(new ScreenRect(0, 0, 1, 1), ScreenPixelFormat.Rgb24, [[White]]);
        using var template = CreateFrame(new ScreenRect(0, 0, 1, 1), ScreenPixelFormat.Rgb24, [[Black]]);

        var match = _matcher.FindMatch(frame, template, new ScreenImageMatchOptions { MinimumSimilarity = 0.0, SelectionMode = selectionMode }, NonCancelableToken);

        Assert.Equal(new ScreenImageMatch(new ScreenPoint(0, 0), 0.0), match);
    }

    [Fact]
    public void FindMatch_WhenSimilarityIsNotFinite_ThrowsArgumentOutOfRangeException()
    {
        using var frame = CreateFrame(new ScreenRect(0, 0, 1, 1), ScreenPixelFormat.Rgb24, [[Black]]);
        using var template = CreateFrame(new ScreenRect(0, 0, 1, 1), ScreenPixelFormat.Rgb24, [[Black]]);

        foreach (var similarity in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                _ = _matcher.FindMatch(frame, template, new ScreenImageMatchOptions { MinimumSimilarity = similarity }, NonCancelableToken);
            });
        }
    }

    [Fact]
    public void FindMatch_WhenSearchRegionIsBounded_ReturnsAbsoluteCoordinateInsideRegion()
    {
        using var frame = CreateFrame(
            new ScreenRect(10, 20, 5, 4),
            ScreenPixelFormat.Bgra8888,
            [
                [Black, Black, Black, Black, Black],
                [Black, Black, Black, Black, Black],
                [Black, Black, Black, Red, Green],
                [Black, Black, Black, Blue, White],
            ]);
        using var template = CreateFrame(
            new ScreenRect(0, 0, 2, 2),
            ScreenPixelFormat.Rgb24,
            [
                [Red, Green],
                [Blue, White],
            ]);

        var match = _matcher.FindMatch(
            frame,
            template,
            new ScreenImageMatchOptions { SearchRegion = new ScreenRect(12, 21, 3, 3) },
            NonCancelableToken);

        Assert.Equal(new ScreenImageMatch(new ScreenPoint(13, 22), 1.0), match);
    }

    [Fact]
    public void FindMatch_WhenTemplateUsesDifferentPixelFormat_NormalizesChannels()
    {
        using var frame = CreateFrame(new ScreenRect(0, 0, 1, 1), ScreenPixelFormat.Abgr8888, [[new ScreenPixelColor(0x12, 0x34, 0x56)]]);
        using var template = CreateFrame(new ScreenRect(0, 0, 1, 1), ScreenPixelFormat.Bgr24, [[new ScreenPixelColor(0x12, 0x34, 0x56)]]);

        var match = _matcher.FindMatch(frame, template, cancellationToken: NonCancelableToken);

        Assert.Equal(new ScreenImageMatch(new ScreenPoint(0, 0), 1.0), match);
    }

    [Theory]
    [InlineData(ScreenImageMatchSelectionMode.FirstThresholdMatch)]
    [InlineData(ScreenImageMatchSelectionMode.BestMatch)]
    public void FindMatch_WhenCancellationIsAlreadyRequested_ThrowsOperationCanceledException(ScreenImageMatchSelectionMode selectionMode)
    {
        using var frame = CreateFrame(new ScreenRect(0, 0, 1, 1), ScreenPixelFormat.Rgb24, [[Black]]);
        using var template = CreateFrame(new ScreenRect(0, 0, 1, 1), ScreenPixelFormat.Rgb24, [[Black]]);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _ = Assert.Throws<OperationCanceledException>(() => _matcher.FindMatch(frame, template, new ScreenImageMatchOptions { SelectionMode = selectionMode }, cts.Token));
    }

    [Theory]
    [InlineData(ScreenImageMatchSelectionMode.FirstThresholdMatch)]
    [InlineData(ScreenImageMatchSelectionMode.BestMatch)]
    public void FindMatch_WhenCancellationIsRequestedDuringMatch_ThrowsOperationCanceledException(ScreenImageMatchSelectionMode selectionMode)
    {
        using var cts = new CancellationTokenSource();
        using var frame = CreateCancellableSolidFrame(new ScreenRect(0, 0, 1024, 128), Black, cts);
        using var template = CreateSolidFrame(new ScreenRect(0, 0, 32, 8), Black);

        _ = Assert.Throws<OperationCanceledException>(() => _matcher.FindMatch(frame, template, new ScreenImageMatchOptions { SelectionMode = selectionMode }, cts.Token));
    }

    [Fact]
    public void FindMatch_WhenCanceledPooledNormalizationIsFollowedBySearch_RemainsCorrect()
    {
        using var cancellation = new CancellationTokenSource();
        using var canceledFrame = CreateCancellableSolidFrame(new ScreenRect(0, 0, 8, 8), Black, cancellation);
        using var template = CreateSolidFrame(new ScreenRect(0, 0, 2, 2), Black);

        _ = Assert.Throws<OperationCanceledException>(() => _matcher.FindMatch(canceledFrame, template, cancellationToken: cancellation.Token));

        using var frame = CreateSolidFrame(new ScreenRect(0, 0, 8, 8), Black);

        Assert.Equal(new ScreenImageMatch(new ScreenPoint(0, 0), 1.0), _matcher.FindMatch(frame, template, new ScreenImageMatchOptions { SelectionMode = ScreenImageMatchSelectionMode.FirstThresholdMatch }));
    }

    [Fact]
    public void FindMatch_WhenMatcherWorkExceedsSingleCandidateBudget_UsesRowBands()
    {
        using var frame = CreateSolidFrame(new ScreenRect(0, 0, 34_000, 10), Black);
        using var template = CreateSolidFrame(new ScreenRect(0, 0, 100, 10), Black);

        var match = _matcher.FindMatch(frame, template, new ScreenImageMatchOptions { SelectionMode = ScreenImageMatchSelectionMode.FirstThresholdMatch }, NonCancelableToken);

        Assert.Equal(new ScreenImageMatch(new ScreenPoint(0, 0), 1.0), match);
        Assert.Equal(1, _matcher.TemplateNormalizationCount);
    }

    [Theory]
    [InlineData(ScreenImageMatchSelectionMode.FirstThresholdMatch)]
    [InlineData(ScreenImageMatchSelectionMode.BestMatch)]
    public void FindMatch_WhenDeterministicSearchExceedsWorkBudget_UsesBoundedAutomaticFallback(ScreenImageMatchSelectionMode selectionMode)
    {
        using var frame = CreateSolidFrame(new ScreenRect(0, 0, 34_000, 10), Black);
        using var template = CreateSolidFrame(new ScreenRect(0, 0, 100, 10), Red);

        var match = _matcher.FindMatch(
            frame,
            template,
            new ScreenImageMatchOptions
            {
                MinimumSimilarity = 0.95,
                SelectionMode = selectionMode,
            },
            NonCancelableToken);

        Assert.Null(match);
        Assert.InRange(_matcher.LastAutomaticSearchDiagnostics.CandidateWork, 0, ScreenImageMatcher.MaxMatcherWork);
    }

    [Theory]
    [InlineData(ScreenImageMatchSelectionMode.FirstThresholdMatch)]
    [InlineData(ScreenImageMatchSelectionMode.BestMatch)]
    public void FindMatch_WhenDeterministicSearchExceedsWorkBudget_FindsTargetWithBoundedAutomaticFallback(ScreenImageMatchSelectionMode selectionMode)
    {
        const int targetX = 700;
        const int targetY = 200;
        var templatePixels = CreatePattern(32, 32);
        var framePixels = Solid(1_000, 400, Black);
        CopyPixels(templatePixels, framePixels, targetX, targetY);
        using var frame = CreateFrame(new ScreenRect(0, 0, 1_000, 400), ScreenPixelFormat.Rgb24, framePixels);
        using var template = CreateFrame(new ScreenRect(0, 0, 32, 32), ScreenPixelFormat.Rgb24, templatePixels);

        var match = _matcher.FindMatch(
            frame,
            template,
            new ScreenImageMatchOptions
            {
                MinimumSimilarity = 0.99,
                SelectionMode = selectionMode,
            },
            NonCancelableToken);

        Assert.Equal(new ScreenPoint(targetX, targetY), match?.Point);
        Assert.InRange(_matcher.LastAutomaticSearchDiagnostics.CandidateWork, 0, ScreenImageMatcher.MaxMatcherWork);
    }

    [Fact]
    public void FindMatch_WhenCoarseSearchReachesBottomRightCandidate_PreservesExactMatch()
    {
        var framePixels = Solid(32, 32, Black);
        var templatePixels = Solid(16, 16, White);
        for (var y = 0; y < templatePixels.Length; y++)
        {
            templatePixels[y].AsSpan().CopyTo(framePixels[16 + y].AsSpan(16, 16));
        }

        using var frame = CreateFrame(new ScreenRect(0, 0, 32, 32), ScreenPixelFormat.Rgb24, framePixels);
        using var template = CreateFrame(new ScreenRect(0, 0, 16, 16), ScreenPixelFormat.Rgb24, templatePixels);

        var match = _matcher.FindMatch(frame, template, new ScreenImageMatchOptions { MinimumSimilarity = 1.0 }, NonCancelableToken);

        Assert.Equal(new ScreenImageMatch(new ScreenPoint(16, 16), 1.0), match);
    }

    [Fact]
    public void FindMatch_FirstThresholdMatch_PreservesRowMajorOrderForLargeTemplates()
    {
        var framePixels = Solid(20, 16, Red);
        framePixels[0][0] = Blue;

        using var frame = CreateFrame(new ScreenRect(0, 0, 20, 16), ScreenPixelFormat.Rgb24, framePixels);
        using var template = CreateFrame(new ScreenRect(0, 0, 16, 16), ScreenPixelFormat.Rgb24, Solid(16, 16, Red));

        var match = _matcher.FindMatch(
            frame,
            template,
            new ScreenImageMatchOptions
            {
                MinimumSimilarity = 0.99,
                SelectionMode = ScreenImageMatchSelectionMode.FirstThresholdMatch,
            },
            NonCancelableToken);

        Assert.Equal(new ScreenPoint(0, 0), match?.Point);
    }

    [Fact]
    public void FindMatch_WhenTemplateIsRepeated_ReusesNormalizedTemplate()
    {
        using var frame = CreateFrame(new ScreenRect(0, 0, 1, 1), ScreenPixelFormat.Rgb24, [[Red]]);
        using var template = CreateFrame(new ScreenRect(0, 0, 1, 1), ScreenPixelFormat.Rgb24, [[Red]]);

        _ = _matcher.FindMatch(frame, template, cancellationToken: NonCancelableToken);
        _ = _matcher.FindMatch(frame, template, cancellationToken: NonCancelableToken);

        Assert.Equal(1, _matcher.TemplateNormalizationCount);
    }

    [Fact]
    public async Task FindMatch_WhenTemplateIsRequestedConcurrently_MaterializesItOnce()
    {
        using var frame = CreateFrame(new ScreenRect(0, 0, 4, 4), ScreenPixelFormat.Rgb24, Solid(4, 4, Red));
        using var template = CreateFrame(new ScreenRect(0, 0, 2, 2), ScreenPixelFormat.Rgb24, Solid(2, 2, Red));

        var searches = Enumerable.Range(0, 8)
            .Select(_ => Task.Run(() => _matcher.FindMatch(frame, template, cancellationToken: NonCancelableToken)))
            .ToArray();
        var matches = await Task.WhenAll(searches);

        Assert.All(matches, match => Assert.Equal(new ScreenImageMatch(new ScreenPoint(0, 0), 1.0), match));
        Assert.Equal(1, _matcher.TemplateNormalizationCount);
    }

    [Fact]
    public void FindMatch_WhenTemplateCacheKeyChanges_DoesNotReuseNormalizedTemplate()
    {
        using var frame = CreateFrame(new ScreenRect(0, 0, 1, 1), ScreenPixelFormat.Rgb24, [[Red]]);
        using var rgbTemplate = CreateFrame(new ScreenRect(0, 0, 1, 1), ScreenPixelFormat.Rgb24, [[Red]]);
        using var differentTemplate = CreateFrame(new ScreenRect(0, 0, 1, 1), ScreenPixelFormat.Rgb24, [[Green]]);
        using var bgrTemplate = CreateFrame(new ScreenRect(0, 0, 1, 1), ScreenPixelFormat.Bgr24, [[Red]]);

        var firstOptions = new ScreenImageMatchOptions { SelectionMode = ScreenImageMatchSelectionMode.FirstThresholdMatch };
        _ = _matcher.FindMatch(frame, rgbTemplate, firstOptions, NonCancelableToken);
        _ = _matcher.FindMatch(frame, differentTemplate, firstOptions, NonCancelableToken);
        _ = _matcher.FindMatch(frame, bgrTemplate, firstOptions, NonCancelableToken);

        Assert.Equal(3, _matcher.TemplateNormalizationCount);
    }

    [Fact]
    public void FindMatch_WhenAlphaMaskOptionChanges_DoesNotReuseDifferentTemplateSemantics()
    {
        using var frame = CreateFrame(new ScreenRect(0, 0, 2, 1), ScreenPixelFormat.Rgb24, [[Blue, Green]]);
        using var template = new ScreenFrame(
            new ScreenRect(0, 0, 2, 1),
            stride: 8,
            ScreenPixelFormat.Abgr8888,
            new byte[]
            {
                0xFF, 0x00, 0x00, 0x00,
                0x00, 0xFF, 0x00, 0xFF,
            },
            alphaMode: ScreenAlphaMode.Straight);

        _ = _matcher.FindMatch(frame, template, cancellationToken: NonCancelableToken);
        _ = _matcher.FindMatch(frame, template, new ScreenImageMatchOptions { UseTemplateAlphaMask = false }, NonCancelableToken);

        Assert.Equal(2, _matcher.TemplateNormalizationCount);
    }

    [Fact]
    public void FindMatch_WhenAlphaMaskIsDisabled_StillComparesTransparentValidPixelsAlongsideInvalidTemplatePixels()
    {
        using var frame = CreateFrame(new ScreenRect(0, 0, 3, 1), ScreenPixelFormat.Rgb24, [[Red, Green, Blue]]);
        using var template = new ScreenFrame(
            new ScreenRect(0, 0, 3, 1),
            stride: 12,
            ScreenPixelFormat.Abgr8888,
            new byte[]
            {
                0x00, 0x00, 0xFF, 0x00,
                0x00, 0xFF, 0x00, 0xFF,
                0x00, 0x00, 0x00, 0xFF,
            },
            validPixelMask: new byte[] { 1, 1, 0 },
            alphaMode: ScreenAlphaMode.Straight);

        var match = _matcher.FindMatch(
            frame,
            template,
            new ScreenImageMatchOptions
            {
                MinimumSimilarity = 1.0,
                SelectionMode = ScreenImageMatchSelectionMode.FirstThresholdMatch,
                UseTemplateAlphaMask = false,
            },
            NonCancelableToken);

        Assert.Null(match);
    }

    [Fact]
    public void FindMatch_IgnoresInvalidTemplatePixelsAlongsideValidPixels()
    {
        using var frame = CreateFrame(new ScreenRect(0, 0, 2, 1), ScreenPixelFormat.Rgb24, [[Red, Green]]);
        using var template = CreateFrame(
            new ScreenRect(0, 0, 2, 1),
            ScreenPixelFormat.Rgb24,
            [[Black, Green]],
            validPixelMask: [0, 1]);

        var match = _matcher.FindMatch(frame, template, cancellationToken: NonCancelableToken);

        Assert.Equal(new ScreenImageMatch(new ScreenPoint(0, 0), 1.0), match);
    }

    [Fact]
    public void FindMatch_OpaqueAlphaBytesDoNotBecomeTemplateMaskWhenValidityMaskExists()
    {
        using var frame = CreateFrame(new ScreenRect(0, 0, 2, 1), ScreenPixelFormat.Rgb24, [[Red, Green]]);
        using var template = new ScreenFrame(
            new ScreenRect(0, 0, 2, 1),
            stride: 8,
            ScreenPixelFormat.Bgra8888,
            new byte[]
            {
                0x00, 0x00, 0x00, 0x00,
                0x00, 0xFF, 0x00, 0x00,
            },
            validPixelMask: new byte[] { 0, 1 },
            alphaMode: ScreenAlphaMode.Opaque);

        var match = _matcher.FindMatch(frame, template, cancellationToken: NonCancelableToken);

        Assert.Equal(new ScreenImageMatch(new ScreenPoint(0, 0), 1.0), match);
    }

    [Fact]
    public void FindMatch_CoarseZeroDoesNotSkipFullResolutionVerification()
    {
        using var frame = CreateFrame(
            new ScreenRect(0, 0, 16, 16),
            ScreenPixelFormat.Rgb24,
            Solid(16, 16, new ScreenPixelColor(128, 0, 0)));
        var templatePixels = Solid(16, 16, Black);
        for (var y = 0; y < 16; y += 2)
        {
            for (var x = 0; x < 16; x += 2)
            {
                templatePixels[y][x] = Red;
                templatePixels[y][x + 1] = Red;
            }
        }
        using var template = CreateFrame(new ScreenRect(0, 0, 16, 16), ScreenPixelFormat.Rgb24, templatePixels);

        var match = _matcher.FindMatch(frame, template, new ScreenImageMatchOptions
        {
            MinimumSimilarity = 1.0,
        }, NonCancelableToken);

        Assert.Null(match);
    }

    [Fact]
    public void FindMatch_WhenTemplateCacheIsFull_EvictsLeastRecentlyUsedTemplate()
    {
        using var matcher = new ScreenImageMatcher(maxTemplateCacheBytes: 6);
        using var frame = CreateFrame(new ScreenRect(0, 0, 1, 1), ScreenPixelFormat.Rgb24, [[Red]]);
        using var firstTemplate = CreateFrame(new ScreenRect(0, 0, 1, 1), ScreenPixelFormat.Rgb24, [[Red]]);
        using var secondTemplate = CreateFrame(new ScreenRect(0, 0, 1, 1), ScreenPixelFormat.Rgb24, [[Green]]);

        _ = matcher.FindMatch(frame, firstTemplate, cancellationToken: NonCancelableToken);
        _ = matcher.FindMatch(frame, secondTemplate, cancellationToken: NonCancelableToken);
        _ = matcher.FindMatch(frame, firstTemplate, cancellationToken: NonCancelableToken);

        Assert.Equal(3, matcher.TemplateNormalizationCount);
    }

    [Fact]
    public void FindMatch_DifferentialOracle_CoversThresholdCoordinatesStrideFormatsAndMask()
    {
        using var frame = CreateFrame(
            new ScreenRect(-3, 7, 6, 4),
            ScreenPixelFormat.Bgra8888,
            [
                [Black, Black, Black, Black, Black, Black],
                [Black, Red, Green, Black, Black, Black],
                [Black, Blue, White, Black, Black, Black],
                [Black, Black, Black, Red, Green, Black],
            ],
            validPixelMask: [1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1],
            stridePadding: 5);
        using var template = CreateFrame(
            new ScreenRect(100, 200, 2, 2),
            ScreenPixelFormat.Bgr24,
            [[Red, Green], [Blue, White]],
            stridePadding: 2);
        var options = new ScreenImageMatchOptions
        {
            SearchRegion = new ScreenRect(-2, 8, 5, 3),
            MinimumSimilarity = 1.0,
        };

        var expected = ScalarScreenImageMatcher.FindMatch(frame, template, options, cancellationToken: NonCancelableToken);
        var actual = _matcher.FindMatch(frame, template, options, NonCancelableToken);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ScalarOracle_FirstThresholdMatch_SelectsFirstRowMajorCandidate()
    {
        using var frame = CreateFrame(
            new ScreenRect(-5, -4, 4, 2),
            ScreenPixelFormat.Rgb24,
            [[new ScreenPixelColor(10, 20, 30), new ScreenPixelColor(10, 20, 31), Black, Black], [Black, Black, Black, Black]]);
        using var template = CreateFrame(
            new ScreenRect(0, 0, 2, 1),
            ScreenPixelFormat.Rgb24,
            [[new ScreenPixelColor(10, 20, 30), new ScreenPixelColor(10, 20, 31)]]);
        var options = new ScreenImageMatchOptions { MinimumSimilarity = 1.0 };

        var match = ScalarScreenImageMatcher.FindMatch(frame, template, options, ScalarMatchSelection.FirstThresholdMatch, NonCancelableToken);

        Assert.Equal(new ScreenPoint(-5, -4), match?.Point);
        _ = Assert.NotNull(match);
        Assert.Equal(1.0, match.Value.Score, precision: 12);
    }

    [Fact]
    public void DifferentialOracle_BestMatch_UsesSadThenYThenXAndRejectsGapCandidates()
    {
        using var frame = CreateFrame(
            new ScreenRect(10, -2, 6, 4),
            ScreenPixelFormat.Xbgr8888,
            [
                [Black, Black, Black, Black, Black, Black],
                [Black, Red, Green, Black, Red, Green],
                [Black, Blue, White, Black, Blue, White],
                [Black, Black, Black, Black, Black, Black],
            ],
            validPixelMask: [1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1],
            stridePadding: 3);
        using var template = CreateFrame(new ScreenRect(0, 0, 2, 2), ScreenPixelFormat.Abgr8888, [[Red, Green], [Blue, White]]);

        var expected = ScalarScreenImageMatcher.FindMatch(frame, template, cancellationToken: NonCancelableToken);
        var actual = _matcher.FindMatch(frame, template, new ScreenImageMatchOptions { SelectionMode = ScreenImageMatchSelectionMode.FirstThresholdMatch }, NonCancelableToken);

        Assert.Equal(expected, actual);
        Assert.Equal(new ScreenPoint(11, -1), actual?.Point);
    }

    [Fact]
    public void DifferentialOracle_NoMatchRemainsObservable()
    {
        using var frame = CreateFrame(new ScreenRect(0, 0, 2, 2), ScreenPixelFormat.Rgb24, [[Red, Black], [Black, Black]]);
        using var template = CreateFrame(new ScreenRect(0, 0, 2, 2), ScreenPixelFormat.Rgb24, [[Red, Green], [Blue, White]]);
        var options = new ScreenImageMatchOptions { SelectionMode = ScreenImageMatchSelectionMode.FirstThresholdMatch };

        Assert.Equal(
            ScalarScreenImageMatcher.FindMatch(frame, template, options, cancellationToken: NonCancelableToken),
            _matcher.FindMatch(frame, template, options, NonCancelableToken));

        using var absent = CreateFrame(new ScreenRect(0, 0, 1, 1), ScreenPixelFormat.Rgb24, [[White]]);
        Assert.Null(ScalarScreenImageMatcher.FindMatch(frame, absent, cancellationToken: NonCancelableToken));
        Assert.Null(_matcher.FindMatch(frame, absent, new ScreenImageMatchOptions { SelectionMode = ScreenImageMatchSelectionMode.FirstThresholdMatch }, NonCancelableToken));
    }

    [Fact]
    public void DifferentialOracle_WhenCallerCancellationIsRequested_RejectsTheSearch()
    {
        using var frame = CreateFrame(new ScreenRect(0, 0, 2, 2), ScreenPixelFormat.Rgb24, Solid(2, 2, Black));
        using var template = CreateFrame(new ScreenRect(0, 0, 1, 1), ScreenPixelFormat.Rgb24, [[Black]]);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        _ = Assert.Throws<OperationCanceledException>(() => ScalarScreenImageMatcher.FindMatch(frame, template, cancellationToken: cancellation.Token));
        _ = Assert.Throws<OperationCanceledException>(() => _matcher.FindMatch(frame, template, cancellationToken: cancellation.Token));
    }

    [Fact]
    public void FindMatch_AutomaticUsesOnlyCoveredTemplatePixelsForValidity()
    {
        using var frame = CreateFrame(
            new ScreenRect(0, 0, 2, 1),
            ScreenPixelFormat.Rgb24,
            [[Black, Green]],
            validPixelMask: [0, 1]);
        using var template = new ScreenFrame(
            new ScreenRect(0, 0, 2, 1),
            stride: 8,
            ScreenPixelFormat.Abgr8888,
            new byte[]
            {
                0xFF, 0x00, 0x00, 0x00,
                0x00, 0xFF, 0x00, 0xFF,
            },
            alphaMode: ScreenAlphaMode.Straight);

        var match = _matcher.FindMatch(frame, template, new ScreenImageMatchOptions
        {
            SelectionMode = ScreenImageMatchSelectionMode.Automatic,
            MinimumSimilarity = 0.99,
        }, NonCancelableToken);

        Assert.Equal(new ScreenImageMatch(new ScreenPoint(0, 0), 1.0), match);
    }

    [Fact]
    public void FindMatch_AutomaticRejectsNearMatchesForLowVarianceTemplates()
    {
        using var frame = CreateFrame(
            new ScreenRect(0, 0, 2, 2),
            ScreenPixelFormat.Rgb24,
            [[new ScreenPixelColor(254, 0, 0), new ScreenPixelColor(254, 0, 0)], [new ScreenPixelColor(254, 0, 0), new ScreenPixelColor(254, 0, 0)]]);
        using var template = CreateSolidFrame(new ScreenRect(0, 0, 2, 2), Red);

        var match = _matcher.FindMatch(frame, template, new ScreenImageMatchOptions
        {
            SelectionMode = ScreenImageMatchSelectionMode.Automatic,
            MinimumSimilarity = 1.0,
        }, NonCancelableToken);

        Assert.Null(match);
    }

    [Fact]
    public void FindMatch_AutomaticDoesNotPromoteWeakPositiveCorrelationToFullConfidence()
    {
        using var frame = CreateFrame(
            new ScreenRect(0, 0, 4, 4),
            ScreenPixelFormat.Rgb24,
            [
                [Black, Black, Black, Black],
                [new ScreenPixelColor(128, 128, 128), new ScreenPixelColor(128, 128, 128), new ScreenPixelColor(128, 128, 128), new ScreenPixelColor(128, 128, 128)],
                [new ScreenPixelColor(128, 128, 128), new ScreenPixelColor(128, 128, 128), new ScreenPixelColor(128, 128, 128), new ScreenPixelColor(128, 128, 128)],
                [White, White, White, White],
            ]);
        using var template = CreateFrame(
            new ScreenRect(0, 0, 4, 4),
            ScreenPixelFormat.Rgb24,
            [
                [Black, Black, Black, Black],
                [Black, Black, Black, Black],
                [White, White, White, White],
                [White, White, White, White],
            ]);

        var match = _matcher.FindMatch(frame, template, new ScreenImageMatchOptions
        {
            SelectionMode = ScreenImageMatchSelectionMode.Automatic,
            MinimumSimilarity = 0.95,
        }, NonCancelableToken);

        Assert.Null(match);
    }

    [Fact]
    public void FindMatch_AutomaticAcceptsGlobalBrightnessShiftWithRgbEvidence()
    {
        var templatePixels = CreatePattern(16, 16);
        var framePixels = Solid(48, 40, Black);
        CopyPixels(AddRgbOffset(templatePixels, 24), framePixels, 17, 11);
        using var frame = CreateFrame(new ScreenRect(0, 0, 48, 40), ScreenPixelFormat.Rgb24, framePixels);
        using var template = CreateFrame(new ScreenRect(0, 0, 16, 16), ScreenPixelFormat.Rgb24, templatePixels);

        var match = _matcher.FindMatch(frame, template, new ScreenImageMatchOptions
        {
            SelectionMode = ScreenImageMatchSelectionMode.Automatic,
            MinimumSimilarity = 0.95,
        }, NonCancelableToken);

        Assert.Equal(new ScreenPoint(17, 11), match?.Point);
        Assert.True(match?.Score >= 0.95);
    }

    [Fact]
    public void FindMatch_AutomaticRejectsLumaOnlyChromaMutation()
    {
        var templatePixels = CreateGrayscalePattern(16, 16);
        var framePixels = Solid(48, 40, Black);
        CopyPixels(RecolorWithNearlyIdenticalLuma(templatePixels), framePixels, 17, 11);
        using var frame = CreateFrame(new ScreenRect(0, 0, 48, 40), ScreenPixelFormat.Rgb24, framePixels);
        using var template = CreateFrame(new ScreenRect(0, 0, 16, 16), ScreenPixelFormat.Rgb24, templatePixels);

        var match = _matcher.FindMatch(frame, template, new ScreenImageMatchOptions
        {
            SelectionMode = ScreenImageMatchSelectionMode.Automatic,
            MinimumSimilarity = 0.95,
        }, NonCancelableToken);

        Assert.Null(match);
    }

    [Fact]
    public void FindMatch_AutomaticRejectsLargeTemplateWithInsufficientEffectiveAlphaCoverage()
    {
        var framePixels = Solid(16, 16, Black);
        framePixels[9][7] = Red;
        using var frame = CreateFrame(new ScreenRect(0, 0, 16, 16), ScreenPixelFormat.Rgb24, framePixels);
        using var template = CreateSparseAlphaTemplate(8, 8, activeX: 3, activeY: 4, Red);

        var match = _matcher.FindMatch(frame, template, new ScreenImageMatchOptions
        {
            SelectionMode = ScreenImageMatchSelectionMode.Automatic,
            MinimumSimilarity = 0.95,
        }, NonCancelableToken);

        Assert.Null(match);
    }

    [Fact]
    public void FindMatch_AutomaticRejectsSpatiallyDistinctTiedCandidates()
    {
        var templatePixels = new[]
        {
            new[] { Red, Green },
            new[] { Blue, White },
        };
        var framePixels = Solid(6, 2, Black);
        CopyPixels(templatePixels, framePixels, 0, 0);
        CopyPixels(templatePixels, framePixels, 4, 0);
        using var frame = CreateFrame(new ScreenRect(0, 0, 6, 2), ScreenPixelFormat.Rgb24, framePixels);
        using var template = CreateFrame(new ScreenRect(0, 0, 2, 2), ScreenPixelFormat.Rgb24, templatePixels);

        var match = _matcher.FindMatch(frame, template, new ScreenImageMatchOptions
        {
            SelectionMode = ScreenImageMatchSelectionMode.Automatic,
            MinimumSimilarity = 1.0,
        }, NonCancelableToken);

        Assert.Null(match);
    }

    [Fact]
    public void FindMatch_AutomaticRefinesLargePyramidCandidateAtFullResolution()
    {
        var templatePixels = CreatePattern(32, 32);
        var framePixels = Solid(120, 100, Black);
        CopyPixels(templatePixels, framePixels, 80, 60);
        using var frame = CreateFrame(new ScreenRect(0, 0, 120, 100), ScreenPixelFormat.Rgb24, framePixels);
        using var template = CreateFrame(new ScreenRect(0, 0, 32, 32), ScreenPixelFormat.Rgb24, templatePixels);

        var match = _matcher.FindMatch(frame, template, new ScreenImageMatchOptions
        {
            SelectionMode = ScreenImageMatchSelectionMode.Automatic,
            MinimumSimilarity = 1.0,
        }, NonCancelableToken);

        Assert.Equal(new ScreenPoint(80, 60), match?.Point);
        Assert.True(match?.Score >= 0.99);
    }

    [Fact]
    public void FindMatch_AutomaticPyramidKeepsSearchingWhenRegionContainsRemoteMonitorGap()
    {
        var templatePixels = CreatePattern(32, 32);
        var framePixels = Solid(120, 100, Black);
        CopyPixels(templatePixels, framePixels, 80, 60);
        var validPixelMask = new byte[120 * 100];
        Array.Fill(validPixelMask, (byte)1);
        for (var y = 0; y < 100; y++)
        {
            validPixelMask[(y * 120) + 10] = 0;
        }

        using var frame = CreateFrame(new ScreenRect(0, 0, 120, 100), ScreenPixelFormat.Rgb24, framePixels, validPixelMask);
        using var template = CreateFrame(new ScreenRect(0, 0, 32, 32), ScreenPixelFormat.Rgb24, templatePixels);

        var match = _matcher.FindMatch(frame, template, new ScreenImageMatchOptions
        {
            SelectionMode = ScreenImageMatchSelectionMode.Automatic,
            MinimumSimilarity = 1.0,
        }, NonCancelableToken);

        Assert.Equal(new ScreenPoint(80, 60), match?.Point);
        Assert.Equal(1, _matcher.TemplatePyramidBuildCount);
        Assert.True(_matcher.LastAutomaticSearchDiagnostics.Work > 0);
    }

    [Fact]
    public void FindMatch_AutomaticRefinesScale()
    {
        var templatePixels = CreatePattern(20, 20);
        var scaledPixels = ResizeFixture(templatePixels, 18, 18);
        var framePixels = Solid(48, 24, Black);
        CopyPixels(scaledPixels, framePixels, 16, 3);
        using var frame = CreateFrame(new ScreenRect(0, 0, 48, 24), ScreenPixelFormat.Rgb24, framePixels);
        using var template = CreateFrame(new ScreenRect(0, 0, 20, 20), ScreenPixelFormat.Rgb24, templatePixels);

        var match = _matcher.FindMatch(frame, template, new ScreenImageMatchOptions
        {
            SelectionMode = ScreenImageMatchSelectionMode.Automatic,
            MinimumSimilarity = 0.99,
        }, NonCancelableToken);

        Assert.Equal(new ScreenPoint(16, 3), match?.Point);
        Assert.Equal(18, match?.MatchedWidth);
        Assert.Equal(18, match?.MatchedHeight);
        Assert.True(match?.Score >= 0.99);
    }

    [Fact]
    public void FindMatch_AutomaticRefinesUpscaledTemplate()
    {
        var templatePixels = CreatePattern(20, 20);
        var scaledPixels = ResizeFixture(templatePixels, 22, 22);
        var framePixels = Solid(56, 28, Black);
        CopyPixels(scaledPixels, framePixels, 16, 3);
        using var frame = CreateFrame(new ScreenRect(0, 0, 56, 28), ScreenPixelFormat.Rgb24, framePixels);
        using var template = CreateFrame(new ScreenRect(0, 0, 20, 20), ScreenPixelFormat.Rgb24, templatePixels);

        var match = _matcher.FindMatch(frame, template, new ScreenImageMatchOptions
        {
            SelectionMode = ScreenImageMatchSelectionMode.Automatic,
            MinimumSimilarity = 0.99,
        }, NonCancelableToken);

        Assert.Equal(new ScreenPoint(16, 3), match?.Point);
        Assert.Equal(22, match?.MatchedWidth);
        Assert.Equal(22, match?.MatchedHeight);
        Assert.True(match?.Score >= 0.99);
    }

    [Fact]
    public void FindMatch_AutomaticSelectsExactScaledCandidateOverThresholdPassingNativeCandidate()
    {
        var templatePixels = CreatePattern(20, 20);
        var framePixels = Solid(80, 30, Black);
        CopyPixels(AddRgbOffset(templatePixels, 10), framePixels, 2, 3);
        CopyPixels(ResizeFixture(templatePixels, 22, 22), framePixels, 50, 3);
        using var frame = CreateFrame(new ScreenRect(0, 0, 80, 30), ScreenPixelFormat.Rgb24, framePixels);
        using var template = CreateFrame(new ScreenRect(0, 0, 20, 20), ScreenPixelFormat.Rgb24, templatePixels);

        var match = _matcher.FindMatch(frame, template, new ScreenImageMatchOptions
        {
            SelectionMode = ScreenImageMatchSelectionMode.Automatic,
            MinimumSimilarity = 0.95,
        }, NonCancelableToken);

        Assert.Equal(new ScreenPoint(50, 3), match?.Point);
        Assert.Equal(22, match?.MatchedWidth);
        Assert.Equal(22, match?.MatchedHeight);
        Assert.Equal(1.0, match?.Score);
    }

    [Fact]
    public void FindMatch_AutomaticCachesTemplatePyramidAndReportsDeterministicCandidateCount()
    {
        var templatePixels = CreatePattern(32, 32);
        var framePixels = Solid(120, 100, Black);
        CopyPixels(templatePixels, framePixels, 80, 60);
        using var frame = CreateFrame(new ScreenRect(0, 0, 120, 100), ScreenPixelFormat.Rgb24, framePixels);
        using var template = CreateFrame(new ScreenRect(0, 0, 32, 32), ScreenPixelFormat.Rgb24, templatePixels);
        var options = new ScreenImageMatchOptions
        {
            SelectionMode = ScreenImageMatchSelectionMode.Automatic,
            MinimumSimilarity = 1.0,
        };

        Assert.Equal(new ScreenPoint(80, 60), _matcher.FindMatch(frame, template, options, NonCancelableToken)?.Point);
        var first = _matcher.LastAutomaticSearchDiagnostics;

        Assert.Equal(1, _matcher.TemplatePyramidBuildCount);
        Assert.True(first.Work > 0);
        Assert.True(first.CandidateCount > 0);

        Assert.Equal(new ScreenPoint(80, 60), _matcher.FindMatch(frame, template, options, NonCancelableToken)?.Point);
        var second = _matcher.LastAutomaticSearchDiagnostics;

        Assert.Equal(1, _matcher.TemplatePyramidBuildCount);
        Assert.Equal(first.CandidateCount, second.CandidateCount);
        Assert.True(second.Work < first.Work);
    }

    [Fact]
    public void FindMatch_Automatic_FullDualTwoKDesktopDoesNotConsumeCandidateBudgetDuringPreparation()
    {
        const int width = 5_120;
        const int height = 1_440;
        const int templateWidth = 32;
        const int templateHeight = 32;
        const int targetX = 4_000;
        const int targetY = 800;
        var frameBytes = new byte[checked(width * height * 3)];
        var templateBytes = new byte[checked(templateWidth * templateHeight * 3)];

        for (var y = 0; y < templateHeight; y++)
        {
            for (var x = 0; x < templateWidth; x++)
            {
                var offset = ((y * templateWidth) + x) * 3;
                templateBytes[offset] = (byte)(20 + ((x * 31 + y * 17) % 220));
                templateBytes[offset + 1] = (byte)(15 + ((x * 13 + y * 29) % 230));
                templateBytes[offset + 2] = (byte)(10 + ((x * 7 + y * 37) % 240));
                var frameOffset = (((targetY + y) * width) + targetX + x) * 3;
                templateBytes.AsSpan(offset, 3).CopyTo(frameBytes.AsSpan(frameOffset, 3));
            }
        }

        using var frame = new ScreenFrame(
            new ScreenRect(0, 0, width, height),
            width * 3,
            ScreenPixelFormat.Rgb24,
            frameBytes);
        using var template = new ScreenFrame(
            new ScreenRect(0, 0, templateWidth, templateHeight),
            templateWidth * 3,
            ScreenPixelFormat.Rgb24,
            templateBytes);

        var match = _matcher.FindMatch(
            frame,
            template,
            new ScreenImageMatchOptions
            {
                SelectionMode = ScreenImageMatchSelectionMode.Automatic,
                MinimumSimilarity = 0.95,
            },
            NonCancelableToken);

        Assert.Equal(new ScreenPoint(targetX, targetY), match?.Point);
        var diagnostics = _matcher.LastAutomaticSearchDiagnostics;
        Assert.True(diagnostics.Work > 0);
        Assert.InRange(diagnostics.CandidateWork, 0, ScreenImageMatcher.MaxMatcherWork);
        Assert.True(diagnostics.PreparationWork > diagnostics.CandidateWork);
    }

    [Fact]
    public void FindMatch_Automatic_FullDualTwoKDesktopSupportsMediumTemplateWithoutCandidateBudgetOverflow()
    {
        const int width = 5_120;
        const int height = 1_440;
        const int templateWidth = 256;
        const int templateHeight = 256;
        const int targetX = 3_600;
        const int targetY = 500;
        var frameBytes = new byte[checked(width * height * 3)];
        var templateBytes = new byte[checked(templateWidth * templateHeight * 3)];

        for (var y = 0; y < templateHeight; y++)
        {
            for (var x = 0; x < templateWidth; x++)
            {
                var offset = ((y * templateWidth) + x) * 3;
                templateBytes[offset] = (byte)(15 + ((x * 31 + y * 17) % 230));
                templateBytes[offset + 1] = (byte)(10 + ((x * 13 + y * 29) % 240));
                templateBytes[offset + 2] = (byte)(5 + ((x * 7 + y * 37) % 250));
                var frameOffset = (((targetY + y) * width) + targetX + x) * 3;
                templateBytes.AsSpan(offset, 3).CopyTo(frameBytes.AsSpan(frameOffset, 3));
            }
        }

        using var frame = new ScreenFrame(
            new ScreenRect(0, 0, width, height),
            width * 3,
            ScreenPixelFormat.Rgb24,
            frameBytes);
        using var template = new ScreenFrame(
            new ScreenRect(0, 0, templateWidth, templateHeight),
            templateWidth * 3,
            ScreenPixelFormat.Rgb24,
            templateBytes);

        var match = _matcher.FindMatch(
            frame,
            template,
            new ScreenImageMatchOptions
            {
                SelectionMode = ScreenImageMatchSelectionMode.Automatic,
                MinimumSimilarity = 0.95,
            },
            NonCancelableToken);

        Assert.Equal(new ScreenPoint(targetX, targetY), match?.Point);
        Assert.InRange(_matcher.LastAutomaticSearchDiagnostics.CandidateWork, 0, ScreenImageMatcher.MaxMatcherWork);
    }

    [Fact]
    public void FindMatch_AutomaticWhenCoarseEvidenceIsUnavailable_ReturnsNoMatchInsteadOfResourceLimit()
    {
        using var frame = CreateSolidFrame(new ScreenRect(0, 0, 34_000, 10), Black);
        using var template = CreateFrame(
            new ScreenRect(0, 0, 100, 10),
            ScreenPixelFormat.Rgb24,
            CreatePattern(100, 10));

        var match = _matcher.FindMatch(
            frame,
            template,
            new ScreenImageMatchOptions
            {
                SelectionMode = ScreenImageMatchSelectionMode.Automatic,
                MinimumSimilarity = 0.95,
            },
            NonCancelableToken);

        Assert.Null(match);
        Assert.InRange(_matcher.LastAutomaticSearchDiagnostics.CandidateWork, 0, ScreenImageMatcher.MaxMatcherWork);
    }

    private static CancellationToken NonCancelableToken => new(canceled: false);
    private static readonly ScreenPixelColor Black = new(0x00, 0x00, 0x00);
    private static readonly ScreenPixelColor Red = new(0xFF, 0x00, 0x00);
    private static readonly ScreenPixelColor Green = new(0x00, 0xFF, 0x00);
    private static readonly ScreenPixelColor Blue = new(0x00, 0x00, 0xFF);
    private static readonly ScreenPixelColor White = new(0xFF, 0xFF, 0xFF);

    private static ScreenPixelColor[][] Solid(int width, int height, ScreenPixelColor color)
    {
        var rows = new ScreenPixelColor[height][];
        for (var y = 0; y < height; y++)
        {
            rows[y] = new ScreenPixelColor[width];
            Array.Fill(rows[y], color);
        }

        return rows;
    }

    private static ScreenPixelColor[][] CreatePattern(int width, int height)
    {
        var pixels = new ScreenPixelColor[height][];
        for (var y = 0; y < height; y++)
        {
            pixels[y] = new ScreenPixelColor[width];
            for (var x = 0; x < width; x++)
            {
                pixels[y][x] = new ScreenPixelColor(
                    (byte)(20 + ((x * 31 + y * 17) % 200)),
                    (byte)(25 + ((x * 13 + y * 29) % 190)),
                    (byte)(30 + ((x * 7 + y * 37) % 180)));
            }
        }

        return pixels;
    }

    private static ScreenPixelColor[][] CreateGrayscalePattern(int width, int height)
    {
        var pixels = new ScreenPixelColor[height][];
        for (var y = 0; y < height; y++)
        {
            pixels[y] = new ScreenPixelColor[width];
            for (var x = 0; x < width; x++)
            {
                var value = (byte)(48 + ((x * 37 + y * 53) % 160));
                pixels[y][x] = new ScreenPixelColor(value, value, value);
            }
        }

        return pixels;
    }

    private static ScreenPixelColor[][] AddRgbOffset(ScreenPixelColor[][] source, int offset)
    {
        var result = new ScreenPixelColor[source.Length][];
        for (var y = 0; y < source.Length; y++)
        {
            result[y] = new ScreenPixelColor[source[y].Length];
            for (var x = 0; x < source[y].Length; x++)
            {
                var pixel = source[y][x];
                result[y][x] = new ScreenPixelColor(
                    ClampByte(pixel.R + offset),
                    ClampByte(pixel.G + offset),
                    ClampByte(pixel.B + offset));
            }
        }

        return result;
    }

    private static ScreenPixelColor[][] RecolorWithNearlyIdenticalLuma(ScreenPixelColor[][] source)
    {
        var result = new ScreenPixelColor[source.Length][];
        for (var y = 0; y < source.Length; y++)
        {
            result[y] = new ScreenPixelColor[source[y].Length];
            for (var x = 0; x < source[y].Length; x++)
            {
                var value = source[y][x].R;
                result[y][x] = new ScreenPixelColor(
                    checked((byte)(value + 30)),
                    checked((byte)(value - 15)),
                    value);
            }
        }

        return result;
    }

    private static ScreenFrame CreateSparseAlphaTemplate(int width, int height, int activeX, int activeY, ScreenPixelColor color)
    {
        var stride = checked(width * 4);
        var bytes = new byte[checked(stride * height)];
        var offset = checked((activeY * stride) + (activeX * 4));
        bytes[offset] = color.R;
        bytes[offset + 1] = color.G;
        bytes[offset + 2] = color.B;
        bytes[offset + 3] = byte.MaxValue;
        return new ScreenFrame(
            new ScreenRect(0, 0, width, height),
            stride,
            ScreenPixelFormat.Abgr8888,
            bytes,
            alphaMode: ScreenAlphaMode.Straight);
    }

    private static byte ClampByte(int value) => (byte)Math.Clamp(value, 0, byte.MaxValue);

    private static ScreenPixelColor[][] ResizeFixture(ScreenPixelColor[][] source, int width, int height)
    {
        var result = new ScreenPixelColor[height][];
        var useAreaResampling = width < source[0].Length || height < source.Length;
        for (var y = 0; y < height; y++)
        {
            result[y] = new ScreenPixelColor[width];
            for (var x = 0; x < width; x++)
            {
                if (useAreaResampling)
                {
                    result[y][x] = AreaAverage(source, width, height, x, y);
                    continue;
                }

                var sourceYPosition = ((y + 0.5) * source.Length / height) - 0.5;
                var sourceYFloor = (int)Math.Floor(sourceYPosition);
                var sourceY = Math.Clamp(sourceYFloor, 0, source.Length - 1);
                var nextY = Math.Clamp(sourceYFloor + 1, 0, source.Length - 1);
                var yFraction = Math.Clamp(sourceYPosition - sourceYFloor, 0.0, 1.0);
                var sourceXPosition = ((x + 0.5) * source[0].Length / width) - 0.5;
                var sourceXFloor = (int)Math.Floor(sourceXPosition);
                var sourceX = Math.Clamp(sourceXFloor, 0, source[0].Length - 1);
                var nextX = Math.Clamp(sourceXFloor + 1, 0, source[0].Length - 1);
                var xFraction = Math.Clamp(sourceXPosition - sourceXFloor, 0.0, 1.0);
                result[y][x] = Interpolate(source[sourceY][sourceX], source[sourceY][nextX], source[nextY][sourceX], source[nextY][nextX], xFraction, yFraction);
            }
        }

        return result;
    }

    private static ScreenPixelColor AreaAverage(ScreenPixelColor[][] source, int width, int height, int x, int y)
    {
        var sourceTop = y * source.Length / (double)height;
        var sourceBottom = (y + 1) * source.Length / (double)height;
        var sourceLeft = x * source[0].Length / (double)width;
        var sourceRight = (x + 1) * source[0].Length / (double)width;
        var firstY = Math.Max(0, (int)Math.Floor(sourceTop));
        var lastY = Math.Min(source.Length - 1, (int)Math.Ceiling(sourceBottom) - 1);
        var firstX = Math.Max(0, (int)Math.Floor(sourceLeft));
        var lastX = Math.Min(source[0].Length - 1, (int)Math.Ceiling(sourceRight) - 1);
        double red = 0;
        double green = 0;
        double blue = 0;
        for (var sourceY = firstY; sourceY <= lastY; sourceY++)
        {
            var yOverlap = Math.Min(sourceBottom, sourceY + 1.0) - Math.Max(sourceTop, sourceY);
            for (var sourceX = firstX; sourceX <= lastX; sourceX++)
            {
                var overlap = yOverlap * (Math.Min(sourceRight, sourceX + 1.0) - Math.Max(sourceLeft, sourceX));
                var pixel = source[sourceY][sourceX];
                red += pixel.R * overlap;
                green += pixel.G * overlap;
                blue += pixel.B * overlap;
            }
        }

        var pixelArea = (source[0].Length / (double)width) * (source.Length / (double)height);
        return new ScreenPixelColor(
            RoundToByte(red / pixelArea),
            RoundToByte(green / pixelArea),
            RoundToByte(blue / pixelArea));
    }

    private static byte RoundToByte(double value) => (byte)Math.Clamp(
        (int)Math.Round(value, MidpointRounding.AwayFromZero),
        0,
        byte.MaxValue);

    private static ScreenPixelColor Interpolate(
        ScreenPixelColor topLeft,
        ScreenPixelColor topRight,
        ScreenPixelColor bottomLeft,
        ScreenPixelColor bottomRight,
        double xFraction,
        double yFraction)
    {
        static byte InterpolateChannel(byte topLeft, byte topRight, byte bottomLeft, byte bottomRight, double xFraction, double yFraction)
        {
            var top = topLeft + ((topRight - topLeft) * xFraction);
            var bottom = bottomLeft + ((bottomRight - bottomLeft) * xFraction);
            return (byte)Math.Clamp((int)Math.Round(top + ((bottom - top) * yFraction), MidpointRounding.AwayFromZero), 0, byte.MaxValue);
        }

        return new ScreenPixelColor(
            InterpolateChannel(topLeft.R, topRight.R, bottomLeft.R, bottomRight.R, xFraction, yFraction),
            InterpolateChannel(topLeft.G, topRight.G, bottomLeft.G, bottomRight.G, xFraction, yFraction),
            InterpolateChannel(topLeft.B, topRight.B, bottomLeft.B, bottomRight.B, xFraction, yFraction));
    }

    private static void CopyPixels(ScreenPixelColor[][] source, ScreenPixelColor[][] target, int targetX, int targetY)
    {
        for (var y = 0; y < source.Length; y++)
        {
            source[y].AsSpan().CopyTo(target[targetY + y].AsSpan(targetX, source[y].Length));
        }
    }

    private static ScreenFrame CreateFrame(
        ScreenRect bounds,
        ScreenPixelFormat pixelFormat,
        ScreenPixelColor[][] pixels,
        byte[]? validPixelMask = null,
        int stridePadding = 0)
    {
        var bytesPerPixel = ScreenFrame.GetBytesPerPixel(pixelFormat);
        var stride = (bounds.Width * bytesPerPixel) + stridePadding;
        var bytes = new byte[stride * bounds.Height];
        for (var y = 0; y < bounds.Height; y++)
        {
            for (var x = 0; x < bounds.Width; x++)
            {
                WritePixel(bytes, (y * stride) + (x * bytesPerPixel), pixelFormat, pixels[y][x]);
            }
        }

        var mask = validPixelMask is null ? ReadOnlyMemory<byte>.Empty : validPixelMask;
        return new ScreenFrame(bounds, stride, pixelFormat, bytes, validPixelMask: mask);
    }

    private static ScreenFrame CreateSolidFrame(ScreenRect bounds, ScreenPixelColor color)
    {
        var stride = bounds.Width * 3;
        var bytes = new byte[stride * bounds.Height];
        for (var y = 0; y < bounds.Height; y++)
        {
            for (var x = 0; x < bounds.Width; x++)
            {
                var offset = (y * stride) + (x * 3);
                bytes[offset] = color.R;
                bytes[offset + 1] = color.G;
                bytes[offset + 2] = color.B;
            }
        }

        return new ScreenFrame(bounds, stride, ScreenPixelFormat.Rgb24, bytes);
    }

    private static ScreenFrame CreateCancellableSolidFrame(ScreenRect bounds, ScreenPixelColor color, CancellationTokenSource cancellationSource)
    {
        var stride = bounds.Width * 3;
        var bytes = new byte[stride * bounds.Height];
        for (var y = 0; y < bounds.Height; y++)
        {
            for (var x = 0; x < bounds.Width; x++)
            {
                var offset = (y * stride) + (x * 3);
                bytes[offset] = color.R;
                bytes[offset + 1] = color.G;
                bytes[offset + 2] = color.B;
            }
        }

        var memory = new CancellationMemoryManager(bytes, cancellationSource);
        return new ScreenFrame(bounds, stride, ScreenPixelFormat.Rgb24, memory.Memory, memory);
    }

    private sealed class CancellationMemoryManager(byte[] bytes, CancellationTokenSource cancellationSource) : MemoryManager<byte>
    {
        private readonly byte[] _bytes = bytes;
        private readonly CancellationTokenSource _cancellationSource = cancellationSource;
        private int _spanAccessCount;

        public override Span<byte> GetSpan()
        {
            if (Interlocked.Increment(ref _spanAccessCount) is 2)
            {
                _cancellationSource.Cancel();
            }

            return _bytes;
        }

        public override MemoryHandle Pin(int elementIndex = 0) => throw new NotSupportedException();

        public override void Unpin()
        {
        }

        protected override void Dispose(bool disposing)
        {
        }
    }

    private static void WritePixel(byte[] target, int offset, ScreenPixelFormat pixelFormat, ScreenPixelColor color)
    {
        switch (pixelFormat)
        {
            case ScreenPixelFormat.Rgb24:
                target[offset] = color.R;
                target[offset + 1] = color.G;
                target[offset + 2] = color.B;
                break;
            case ScreenPixelFormat.Bgr24:
                target[offset] = color.B;
                target[offset + 1] = color.G;
                target[offset + 2] = color.R;
                break;
            case ScreenPixelFormat.Xrgb8888:
            case ScreenPixelFormat.Bgra8888:
                target[offset] = color.B;
                target[offset + 1] = color.G;
                target[offset + 2] = color.R;
                target[offset + 3] = 0xFF;
                break;
            case ScreenPixelFormat.Abgr8888:
            case ScreenPixelFormat.Xbgr8888:
                target[offset] = color.R;
                target[offset + 1] = color.G;
                target[offset + 2] = color.B;
                target[offset + 3] = 0xFF;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(pixelFormat), pixelFormat, "Unsupported screen pixel format.");
        }
    }
}
