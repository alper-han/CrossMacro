using System.Buffers;

namespace CrossMacro.Infrastructure.Tests.Services.ScreenReading;

using CrossMacro.Infrastructure.Services.ScreenReading;
using CrossMacro.Platform.Abstractions;

public sealed class ScreenImageMatcherTests
{
    private readonly ScreenImageMatcher _matcher = new();

    [Fact]
    public void ScreenImageMatchOptions_WhenSelectionModeIsOmitted_UsesLegacyFirstThresholdMatch()
    {
        var options = new ScreenImageMatchOptions();

        Assert.Equal(ScreenImageMatchSelectionMode.FirstThresholdMatch, options.SelectionMode);
        Assert.Equal(ScreenImageMatchSelectionMode.FirstThresholdMatch, ScreenImageMatchOptions.Default.SelectionMode);
    }

    [Theory]
    [InlineData(ScreenImageMatchSelectionMode.FirstThresholdMatch)]
    [InlineData(ScreenImageMatchSelectionMode.BestMatch)]
    public void ScreenImageMatchOptions_Create_PreservesExplicitSelectionMode(ScreenImageMatchSelectionMode selectionMode)
    {
        var options = ScreenImageMatchOptions.Create(null, 1.0, 1, selectionMode);

        Assert.Equal(selectionMode, options.SelectionMode);
    }

    [Fact]
    public void FindMatch_WhenSelectionModeIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        using var frame = CreateFrame(new ScreenRect(0, 0, 1, 1), ScreenPixelFormat.Rgb24, [[Black]]);
        using var template = CreateFrame(new ScreenRect(0, 0, 1, 1), ScreenPixelFormat.Rgb24, [[Black]]);
        var options = new ScreenImageMatchOptions
        {
            SelectionMode = (ScreenImageMatchSelectionMode)99
        };

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => _matcher.FindMatch(frame, template, options));

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
                [Black, Blue, White, Black]
            ]);
        using var template = CreateFrame(
            new ScreenRect(0, 0, 2, 2),
            ScreenPixelFormat.Rgb24,
            [
                [Red, Green],
                [Blue, White]
            ]);

        var match = _matcher.FindMatch(frame, template);

        Assert.Equal(new ScreenImageMatch(new ScreenPoint(1, 1), 1.0), match);
    }

    [Fact]
    public void FindMatch_ScaleAwareMatchesScaledTemplateAndReportsMatchedDimensions()
    {
        using var frame = CreateFrame(
            new ScreenRect(10, 20, 6, 5),
            ScreenPixelFormat.Rgb24,
            [
                [Black, Black, Black, Black, Black, Black],
                [Black, Red, Red, Green, Black, Black],
                [Black, Red, Red, Green, Black, Black],
                [Black, Blue, Blue, White, Black, Black],
                [Black, Black, Black, Black, Black, Black]
            ]);
        using var template = CreateFrame(
            new ScreenRect(0, 0, 2, 2),
            ScreenPixelFormat.Rgb24,
            [[Red, Green], [Blue, White]]);

        var match = _matcher.FindMatch(frame, template, new ScreenImageMatchOptions
        {
            ScaleAware = true,
            MinimumSimilarity = 1.0
        });

        Assert.Equal(new ScreenPoint(12, 22), match?.Point);
        Assert.Equal(2, match?.MatchedWidth);
        Assert.Equal(2, match?.MatchedHeight);
        Assert.Equal(1.0, match?.Score);
    }

    [Fact]
    public void FindMatch_ScaleAwareWithNonZeroFrameOrigin_UsesLocalCoordinates()
    {
        using var frame = CreateSolidFrame(new ScreenRect(10, 20, 32, 32), Red);
        using var template = CreateSolidFrame(new ScreenRect(0, 0, 16, 16), Red);

        var match = _matcher.FindMatch(frame, template, new ScreenImageMatchOptions
        {
            ScaleAware = true,
            MinimumSimilarity = 1.0
        });

        Assert.Equal(new ScreenPoint(10, 20), match?.Point);
        Assert.Equal(16, match?.MatchedWidth);
        Assert.Equal(16, match?.MatchedHeight);
    }

    [Fact]
    public void FindMatch_ScaleAwareDisabledPreservesCoreResultAndDoesNotNormalizeScaleVariants()
    {
        using var frame = CreateFrame(new ScreenRect(0, 0, 2, 2), ScreenPixelFormat.Rgb24, [[Red, Green], [Blue, White]]);
        using var template = CreateFrame(new ScreenRect(0, 0, 2, 2), ScreenPixelFormat.Rgb24, [[Red, Green], [Blue, White]]);

        var disabled = new ScreenImageMatcher();
        var enabled = new ScreenImageMatcher();
        var core = disabled.FindMatch(frame, template);
        var optIn = enabled.FindMatch(frame, template, new ScreenImageMatchOptions { ScaleAware = true });

        Assert.Equal(core, disabled.FindMatch(frame, template));
        Assert.Equal(core?.Point, optIn?.Point);
        Assert.Equal(1, disabled.TemplateNormalizationCount);
    }

    [Fact]
    public void FindMatch_ScaleAwareDoesNotMatchUnsupportedScaleFixture()
    {
        using var frame = CreateFrame(new ScreenRect(0, 0, 4, 4), ScreenPixelFormat.Rgb24, Solid(4, 4, Black));
        using var template = CreateFrame(new ScreenRect(0, 0, 2, 2), ScreenPixelFormat.Rgb24, [[Red, Green], [Blue, White]]);

        Assert.Null(_matcher.FindMatch(frame, template, new ScreenImageMatchOptions { ScaleAware = true }));
    }

    [Fact]
    public void FindMatch_ScaleAwareSamePointTiePrefersScaleClosestToOne()
    {
        using var frame = CreateFrame(new ScreenRect(0, 0, 4, 4), ScreenPixelFormat.Rgb24, Solid(4, 4, Black));
        using var template = CreateFrame(new ScreenRect(0, 0, 2, 2), ScreenPixelFormat.Rgb24, Solid(2, 2, Black));

        var match = _matcher.FindMatch(frame, template, new ScreenImageMatchOptions { ScaleAware = true });

        Assert.Equal(new ScreenPoint(0, 0), match?.Point);
        Assert.Equal(2, match?.MatchedWidth);
        Assert.Equal(2, match?.MatchedHeight);
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
                [Black, Black, Black, Blue, White]
            ]);
        using var template = CreateFrame(
            new ScreenRect(0, 0, 2, 2),
            ScreenPixelFormat.Rgb24,
            [
                [Red, Green],
                [Blue, White]
            ]);

        for (var attempt = 0; attempt < 10; attempt++)
        {
            var match = _matcher.FindMatch(frame, template);

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
            SelectionMode = ScreenImageMatchSelectionMode.FirstThresholdMatch
        };
        var bestOptions = firstOptions with { SelectionMode = ScreenImageMatchSelectionMode.BestMatch };

        var expectedFirst = ScalarScreenImageMatcher.FindMatch(
            frame,
            template,
            firstOptions,
            ScalarMatchSelection.FirstThresholdMatch);
        var expectedBest = ScalarScreenImageMatcher.FindMatch(
            frame,
            template,
            bestOptions,
            ScalarMatchSelection.BestMatch);

        Assert.Equal(new ScreenPoint(-7, -3), expectedFirst?.Point);
        Assert.Equal(new ScreenPoint(-6, -3), expectedBest?.Point);
        Assert.Equal(expectedFirst, _matcher.FindMatch(frame, template, firstOptions));
        Assert.Equal(expectedBest, _matcher.FindMatch(frame, template, bestOptions));
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
                var selection = selectionMode == ScreenImageMatchSelectionMode.FirstThresholdMatch
                    ? ScalarMatchSelection.FirstThresholdMatch
                    : ScalarMatchSelection.BestMatch;
                var expected = ScalarScreenImageMatcher.FindMatch(frame, template, options, selection);

                Assert.Equal(expected, _matcher.FindMatch(frame, template, options));
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

        var match = _matcher.FindMatch(frame, template, new ScreenImageMatchOptions { SelectionMode = selectionMode });

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

        var match = _matcher.FindMatch(frame, template, new ScreenImageMatchOptions { SelectionMode = selectionMode });

        Assert.Equal(new ScreenImageMatch(new ScreenPoint(1, 0), 1.0), match);
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

        var match = _matcher.FindMatch(frame, template, new ScreenImageMatchOptions { SelectionMode = selectionMode });

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
                [Black, Blue]
            ]);
        using var template = CreateFrame(new ScreenRect(0, 0, 2, 2), ScreenPixelFormat.Rgb24, Solid(2, 2, Black));

        var match = _matcher.FindMatch(frame, template, new ScreenImageMatchOptions { MinimumSimilarity = 0.95 });

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
                [Black, Blue]
            ]);
        using var template = CreateFrame(new ScreenRect(0, 0, 2, 2), ScreenPixelFormat.Rgb24, Solid(2, 2, Black));

        var match = _matcher.FindMatch(frame, template, new ScreenImageMatchOptions { MinimumSimilarity = 0.9 });

		Assert.NotNull(match);
		Assert.Equal(new ScreenPoint(0, 0), match.Value.Point);
		Assert.Equal(1.0 - 255.0 / (2 * 2 * 3 * 255.0), match.Value.Score, precision: 12);
	}

    [Fact]
    public void FindMatch_WhenSadEqualsIntegerThreshold_IncludesCandidate()
    {
        using var frame = CreateFrame(new ScreenRect(0, 0, 1, 1), ScreenPixelFormat.Rgb24, [[Blue]]);
        using var template = CreateFrame(new ScreenRect(0, 0, 1, 1), ScreenPixelFormat.Rgb24, [[Black]]);

        var match = _matcher.FindMatch(frame, template, new ScreenImageMatchOptions { MinimumSimilarity = 0.0 });

        Assert.Equal(new ScreenImageMatch(new ScreenPoint(0, 0), 1.0 - 255.0 / 765.0), match);
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
            SelectionMode = ScreenImageMatchSelectionMode.BestMatch
        });

        Assert.Equal(new ScreenImageMatch(new ScreenPoint(1, 0), 1.0), match);
    }

    [Theory]
    [InlineData(ScreenImageMatchSelectionMode.FirstThresholdMatch)]
    [InlineData(ScreenImageMatchSelectionMode.BestMatch)]
    public void FindMatch_WhenSimilarityIsZero_AllowsTheBoundaryValue(ScreenImageMatchSelectionMode selectionMode)
    {
        using var frame = CreateFrame(new ScreenRect(0, 0, 1, 1), ScreenPixelFormat.Rgb24, [[White]]);
        using var template = CreateFrame(new ScreenRect(0, 0, 1, 1), ScreenPixelFormat.Rgb24, [[Black]]);

        var match = _matcher.FindMatch(frame, template, new ScreenImageMatchOptions { MinimumSimilarity = 0.0, SelectionMode = selectionMode });

        Assert.Equal(new ScreenImageMatch(new ScreenPoint(0, 0), 0.0), match);
    }

	[Fact]
	public void FindMatch_WhenSimilarityIsNotFinite_ThrowsArgumentOutOfRangeException()
	{
		using var frame = CreateFrame(new ScreenRect(0, 0, 1, 1), ScreenPixelFormat.Rgb24, [[Black]]);
		using var template = CreateFrame(new ScreenRect(0, 0, 1, 1), ScreenPixelFormat.Rgb24, [[Black]]);

		foreach (var similarity in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
		{
			Assert.Throws<ArgumentOutOfRangeException>(() =>
			{
				_ = _matcher.FindMatch(frame, template, new ScreenImageMatchOptions { MinimumSimilarity = similarity });
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
                [Black, Black, Black, Blue, White]
            ]);
        using var template = CreateFrame(
            new ScreenRect(0, 0, 2, 2),
            ScreenPixelFormat.Rgb24,
            [
                [Red, Green],
                [Blue, White]
            ]);

        var match = _matcher.FindMatch(
            frame,
            template,
            new ScreenImageMatchOptions { SearchRegion = new ScreenRect(12, 21, 3, 3) });

        Assert.Equal(new ScreenImageMatch(new ScreenPoint(13, 22), 1.0), match);
    }

    [Fact]
    public void FindMatch_WhenDownsampleFactorSkipsChangedPixels_MatchesSampledPixelsOnly()
    {
        using var frame = CreateFrame(
            new ScreenRect(0, 0, 2, 2),
            ScreenPixelFormat.Rgb24,
            [
                [Red, Black],
                [Black, Black]
            ]);
        using var template = CreateFrame(
            new ScreenRect(0, 0, 2, 2),
            ScreenPixelFormat.Rgb24,
            [
                [Red, Green],
                [Blue, White]
            ]);

        var exactMatch = _matcher.FindMatch(frame, template);
        var downsampledMatch = _matcher.FindMatch(frame, template, new ScreenImageMatchOptions { DownsampleFactor = 2 });

        Assert.Null(exactMatch);
        Assert.Equal(new ScreenImageMatch(new ScreenPoint(0, 0), 1.0), downsampledMatch);
    }

    [Fact]
    public void FindMatch_WhenTemplateUsesDifferentPixelFormat_NormalizesChannels()
    {
        using var frame = CreateFrame(new ScreenRect(0, 0, 1, 1), ScreenPixelFormat.Abgr8888, [[new ScreenPixelColor(0x12, 0x34, 0x56)]]);
        using var template = CreateFrame(new ScreenRect(0, 0, 1, 1), ScreenPixelFormat.Bgr24, [[new ScreenPixelColor(0x12, 0x34, 0x56)]]);

        var match = _matcher.FindMatch(frame, template);

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

        Assert.Throws<OperationCanceledException>(() => _matcher.FindMatch(frame, template, new ScreenImageMatchOptions { SelectionMode = selectionMode }, cts.Token));
    }

    [Theory]
    [InlineData(ScreenImageMatchSelectionMode.FirstThresholdMatch)]
    [InlineData(ScreenImageMatchSelectionMode.BestMatch)]
    public void FindMatch_WhenCancellationIsRequestedDuringMatch_ThrowsOperationCanceledException(ScreenImageMatchSelectionMode selectionMode)
    {
        using var cts = new CancellationTokenSource();
        using var frame = CreateCancellableSolidFrame(new ScreenRect(0, 0, 1024, 128), Black, cts);
        using var template = CreateSolidFrame(new ScreenRect(0, 0, 32, 8), Black);

        Assert.Throws<OperationCanceledException>(() => _matcher.FindMatch(frame, template, new ScreenImageMatchOptions { SelectionMode = selectionMode }, cts.Token));
    }

    [Fact]
    public void FindMatch_WhenCanceledPooledNormalizationIsFollowedBySearch_RemainsCorrect()
    {
        using var cancellation = new CancellationTokenSource();
        using var canceledFrame = CreateCancellableSolidFrame(new ScreenRect(0, 0, 8, 8), Black, cancellation);
        using var template = CreateSolidFrame(new ScreenRect(0, 0, 2, 2), Black);

        Assert.Throws<OperationCanceledException>(() => _matcher.FindMatch(canceledFrame, template, cancellationToken: cancellation.Token));

        using var frame = CreateSolidFrame(new ScreenRect(0, 0, 8, 8), Black);

        Assert.Equal(new ScreenImageMatch(new ScreenPoint(0, 0), 1.0), _matcher.FindMatch(frame, template));
    }

    [Fact]
    public void FindMatch_WhenMatcherWorkExceedsSingleCandidateBudget_UsesRowBands()
    {
        using var frame = CreateSolidFrame(new ScreenRect(0, 0, 34_000, 10), Black);
        using var template = CreateSolidFrame(new ScreenRect(0, 0, 100, 10), Black);

        var match = _matcher.FindMatch(frame, template);

        Assert.Equal(new ScreenImageMatch(new ScreenPoint(0, 0), 1.0), match);
        Assert.Equal(1, _matcher.TemplateNormalizationCount);
    }

    [Fact]
    public void FindMatch_WhenTemplateIsRepeated_ReusesNormalizedTemplate()
    {
        using var frame = CreateFrame(new ScreenRect(0, 0, 1, 1), ScreenPixelFormat.Rgb24, [[Red]]);
        using var template = CreateFrame(new ScreenRect(0, 0, 1, 1), ScreenPixelFormat.Rgb24, [[Red]]);

        _matcher.FindMatch(frame, template);
        _matcher.FindMatch(frame, template);

        Assert.Equal(1, _matcher.TemplateNormalizationCount);
    }

    [Fact]
    public void FindMatch_WhenTemplateCacheKeyChanges_DoesNotReuseNormalizedTemplate()
    {
        using var frame = CreateFrame(new ScreenRect(0, 0, 1, 1), ScreenPixelFormat.Rgb24, [[Red]]);
        using var rgbTemplate = CreateFrame(new ScreenRect(0, 0, 1, 1), ScreenPixelFormat.Rgb24, [[Red]]);
        using var differentTemplate = CreateFrame(new ScreenRect(0, 0, 1, 1), ScreenPixelFormat.Rgb24, [[Green]]);
        using var bgrTemplate = CreateFrame(new ScreenRect(0, 0, 1, 1), ScreenPixelFormat.Bgr24, [[Red]]);

        _matcher.FindMatch(frame, rgbTemplate);
        _matcher.FindMatch(frame, differentTemplate);
        _matcher.FindMatch(frame, rgbTemplate, new ScreenImageMatchOptions { DownsampleFactor = 2 });
        _matcher.FindMatch(frame, bgrTemplate);

        Assert.Equal(4, _matcher.TemplateNormalizationCount);
    }

    [Fact]
    public void FindMatch_WhenTemplateCacheIsFull_EvictsLeastRecentlyUsedTemplate()
    {
        using var matcher = new ScreenImageMatcher(maxTemplateCacheBytes: 6);
        using var frame = CreateFrame(new ScreenRect(0, 0, 1, 1), ScreenPixelFormat.Rgb24, [[Red]]);
        using var firstTemplate = CreateFrame(new ScreenRect(0, 0, 1, 1), ScreenPixelFormat.Rgb24, [[Red]]);
        using var secondTemplate = CreateFrame(new ScreenRect(0, 0, 1, 1), ScreenPixelFormat.Rgb24, [[Green]]);

        matcher.FindMatch(frame, firstTemplate);
        matcher.FindMatch(frame, secondTemplate);
        matcher.FindMatch(frame, firstTemplate);

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
                [Black, Black, Black, Red, Green, Black]
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

        var expected = ScalarScreenImageMatcher.FindMatch(frame, template, options);
        var actual = _matcher.FindMatch(frame, template, options);

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

        var match = ScalarScreenImageMatcher.FindMatch(frame, template, options, ScalarMatchSelection.FirstThresholdMatch);

        Assert.Equal(new ScreenPoint(-5, -4), match?.Point);
        Assert.NotNull(match);
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
                [Black, Black, Black, Black, Black, Black]
            ],
            validPixelMask: [1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1],
            stridePadding: 3);
        using var template = CreateFrame(new ScreenRect(0, 0, 2, 2), ScreenPixelFormat.Abgr8888, [[Red, Green], [Blue, White]]);

        var expected = ScalarScreenImageMatcher.FindMatch(frame, template);
        var actual = _matcher.FindMatch(frame, template);

        Assert.Equal(expected, actual);
        Assert.Equal(new ScreenPoint(11, -1), actual?.Point);
    }

    [Fact]
    public void DifferentialOracle_DownsampleAndNoMatchRemainObservable()
    {
        using var frame = CreateFrame(new ScreenRect(0, 0, 2, 2), ScreenPixelFormat.Rgb24, [[Red, Black], [Black, Black]]);
        using var template = CreateFrame(new ScreenRect(0, 0, 2, 2), ScreenPixelFormat.Rgb24, [[Red, Green], [Blue, White]]);
        var options = new ScreenImageMatchOptions { DownsampleFactor = 2 };

        Assert.Equal(
            ScalarScreenImageMatcher.FindMatch(frame, template, options),
            _matcher.FindMatch(frame, template, options));

        using var absent = CreateFrame(new ScreenRect(0, 0, 1, 1), ScreenPixelFormat.Rgb24, [[White]]);
        Assert.Null(ScalarScreenImageMatcher.FindMatch(frame, absent));
        Assert.Null(_matcher.FindMatch(frame, absent));
    }

    [Fact]
    public void DifferentialOracle_WhenCallerCancellationIsRequested_RejectsTheSearch()
    {
        using var frame = CreateFrame(new ScreenRect(0, 0, 2, 2), ScreenPixelFormat.Rgb24, Solid(2, 2, Black));
        using var template = CreateFrame(new ScreenRect(0, 0, 1, 1), ScreenPixelFormat.Rgb24, [[Black]]);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() => ScalarScreenImageMatcher.FindMatch(frame, template, cancellationToken: cancellation.Token));
        Assert.Throws<OperationCanceledException>(() => _matcher.FindMatch(frame, template, cancellationToken: cancellation.Token));
    }

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

    private static ScreenFrame CreateFrame(
        ScreenRect bounds,
        ScreenPixelFormat pixelFormat,
        ScreenPixelColor[][] pixels,
        byte[]? validPixelMask = null,
        int stridePadding = 0)
    {
        var bytesPerPixel = ScreenFrame.GetBytesPerPixel(pixelFormat);
        var stride = bounds.Width * bytesPerPixel + stridePadding;
        var bytes = new byte[stride * bounds.Height];
        for (var y = 0; y < bounds.Height; y++)
        {
            for (var x = 0; x < bounds.Width; x++)
            {
                WritePixel(bytes, y * stride + x * bytesPerPixel, pixelFormat, pixels[y][x]);
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
                var offset = y * stride + x * 3;
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
                var offset = y * stride + x * 3;
                bytes[offset] = color.R;
                bytes[offset + 1] = color.G;
                bytes[offset + 2] = color.B;
            }
        }

        var memory = new CancellationMemoryManager(bytes, cancellationSource);
        return new ScreenFrame(bounds, stride, ScreenPixelFormat.Rgb24, memory.Memory, memory);
    }

    private sealed class CancellationMemoryManager : MemoryManager<byte>
    {
        private readonly byte[] _bytes;
        private readonly CancellationTokenSource _cancellationSource;
        private int _spanAccessCount;

        public CancellationMemoryManager(byte[] bytes, CancellationTokenSource cancellationSource)
        {
            _bytes = bytes;
            _cancellationSource = cancellationSource;
        }

        public override Span<byte> GetSpan()
        {
            if (Interlocked.Increment(ref _spanAccessCount) == 2)
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
