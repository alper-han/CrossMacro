
namespace CrossMacro.Cli.Tests;

public sealed class PrimitiveCliServiceTests
{
    [Fact]
    public async Task Clipboard_Get_ReturnsClipboardTextData()
    {
        var clipboard = new FakeClipboardService { Text = "hello" };
        var service = new ClipboardCliService(clipboard);

        var result = await service.GetAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("hello", Assert.IsType<ClipboardTextData>(result.Data).Value);
    }

    [Fact]
    public async Task Clipboard_SetFile_ReadsFileAndSetsText()
    {
        var path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, "from file");
        try
        {
            var clipboard = new FakeClipboardService();
            var service = new ClipboardCliService(clipboard);

            var result = await service.SetFileAsync(path, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal("from file", clipboard.Text);
            Assert.Equal(9, Assert.IsType<ClipboardSetData>(result.Data).Length);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Clipboard_Clear_SetsEmptyClipboardText()
    {
        var clipboard = new FakeClipboardService { Text = "hello" };
        var service = new ClipboardCliService(clipboard);

        var result = await service.ClearAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(string.Empty, clipboard.Text);
        var data = Assert.IsType<ClipboardSetData>(result.Data);
        Assert.Equal(0, data.Length);
        Assert.Equal("clear", data.Source);
    }

    [Fact]
    public async Task Clipboard_Unsupported_ReturnsEnvironmentError()
    {
        var service = new ClipboardCliService(new FakeClipboardService { IsSupported = false });

        var result = await service.GetAsync(CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal((int)CliExitCode.EnvironmentError, result.ExitCode);
        Assert.Contains("not supported", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Window_SearchAndFocus_UseSubstringSelectorsAndMutator()
    {
        var manager = new FakeWindowManager
        {
            Windows =
            [
                new WindowInfo { Address = "0x1", Title = "Docs - Firefox", Class = "firefox" },
                new WindowInfo { Address = "0x2", Title = "Editor", Class = "Code" }
            ],
        };
        var service = new WindowCliService(manager);

        var search = await service.ExecuteAsync(new WindowCliOptions(WindowCliAction.Search, new WindowSelector(WindowSelectorKind.Title, "fire")), CancellationToken.None);
        var focus = await service.ExecuteAsync(new WindowCliOptions(WindowCliAction.Focus, new WindowSelector(WindowSelectorKind.Class, "code")), CancellationToken.None);

        Assert.True(search.Success);
        var data = Assert.IsType<WindowListData>(search.Data);
        Assert.Equal("0x1", Assert.Single(data.Windows).Address);
        Assert.True(focus.Success);
        Assert.Equal("code", manager.FocusedClass);
    }

    [Fact]
    public async Task Window_NullManager_ReturnsUnsupportedEnvironmentError()
    {
        var manager = new NullWindowManager();
        var service = new WindowCliService(manager);

        var result = await service.ExecuteAsync(new WindowCliOptions(WindowCliAction.List), CancellationToken.None);

        Assert.False(manager.IsSupported);
        Assert.False(result.Success);
        Assert.Equal((int)CliExitCode.EnvironmentError, result.ExitCode);
    }

    [Fact]
    public async Task Window_InvalidActionOptions_ReturnInvalidArguments()
    {
        var service = new WindowCliService(new FakeWindowManager());

        var missingSelector = await service.ExecuteAsync(new WindowCliOptions(WindowCliAction.Search), CancellationToken.None);
        var missingCoordinates = await service.ExecuteAsync(new WindowCliOptions(WindowCliAction.Move, X: 1), CancellationToken.None);
        var unsupportedCloseSelector = await service.ExecuteAsync(new WindowCliOptions(WindowCliAction.Close, new WindowSelector(WindowSelectorKind.Class, "code")), CancellationToken.None);

        AssertInvalidArguments(missingSelector);
        AssertInvalidArguments(missingCoordinates);
        AssertInvalidArguments(unsupportedCloseSelector);
    }

    [Fact]
    public async Task Screen_PixelRelative_UsesMousePositionProvider()
    {
        var reader = new FakeScreenPixelReader();
        var service = new ScreenCliService(reader, new FakeMousePositionProvider { Position = (100, 200) });

        var result = await service.ExecuteAsync(new ScreenCliOptions(ScreenCliAction.Pixel, 5, -10, Relative: true, TimeoutMs: 125), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(new ScreenPoint(105, 190), reader.LastPoint);
        Assert.Equal(TimeSpan.FromMilliseconds(125), reader.LastPixelOptions.Timeout);
        var data = Assert.IsType<ScreenPixelData>(result.Data);
        Assert.Equal("123456", data.Color);
        Assert.True(data.Relative);
    }

    [Fact]
    public async Task Screen_WaitAndSearch_ForwardOptionsToReader()
    {
        var reader = new FakeScreenPixelReader();
        var service = new ScreenCliService(reader, new FakeMousePositionProvider());

        var wait = await service.ExecuteAsync(new ScreenCliOptions(ScreenCliAction.WaitColor, 1, 2, new ScreenPixelColor(0, 255, 0), TimeoutMs: 250), CancellationToken.None);
        var search = await service.ExecuteAsync(new ScreenCliOptions(ScreenCliAction.SearchColor, 0, 0, new ScreenPixelColor(255, 0, 0), X2: 10, Y2: 20, Tolerance: 26, TimeoutMs: 275), CancellationToken.None);

        Assert.True(wait.Success);
        Assert.Equal(TimeSpan.FromMilliseconds(250), reader.LastWaitOptions.Timeout);
        Assert.True(search.Success);
        Assert.Equal(new ScreenRect(0, 0, 10, 20), reader.LastRegion);
        Assert.Equal(26, reader.LastTolerance);
        Assert.Equal(TimeSpan.FromMilliseconds(275), reader.LastSearchOptions.Timeout);
    }

	[Fact]
	public async Task Screen_SearchImage_DecodesPngAndForwardsOptionsToReader()
	{
        var imagePath = await WriteTemplatePngAsync();
        try
        {
            var reader = new FakeScreenPixelReader
            {
                ImageMatch = new ScreenImageMatch(new ScreenPoint(7, 8), 0.95),
            };
            var service = new ScreenCliService(reader, new FakeMousePositionProvider());

            var result = await service.ExecuteAsync(new ScreenCliOptions(
                ScreenCliAction.SearchImage,
                ImagePath: imagePath,
                RegionX: 1,
                RegionY: 2,
                RegionWidth: 30,
                RegionHeight: 40,
                TimeoutMs: 123,
                Similarity: 0.9,
                Downsample: 2,
                MatchMode: ScreenImageMatchMode.Best), CancellationToken.None);

            Assert.True(result.Success, string.Join("; ", result.Errors));
            Assert.Equal(new ScreenRect(1, 2, 30, 40), reader.LastImageRegion);
            Assert.NotNull(reader.LastImageTemplate);
            Assert.Equal(1, reader.LastImageTemplate.Width);
            Assert.Equal(1, reader.LastImageTemplate.Height);
            Assert.Equal(TimeSpan.FromMilliseconds(123), reader.LastImageReadOptions.Timeout);
            Assert.Equal(0.9, reader.LastImageOptions.MinimumSimilarity);
            Assert.Equal(2, reader.LastImageOptions.DownsampleFactor);
            Assert.Equal(ScreenImageMatchSelectionMode.BestMatch, reader.LastImageOptions.SelectionMode);
            var data = Assert.IsType<ScreenSearchImageData>(result.Data);
            Assert.True(data.Found);
            Assert.Equal(7, data.X);
            Assert.Equal(8, data.Y);
            Assert.Equal(0.95, data.Score);
            Assert.Equal(Path.GetFullPath(imagePath), data.ImagePath);
            Assert.Equal("best", data.MatchMode);
        }
        finally
        {
            File.Delete(imagePath);
		}
	}

	[Fact]
	public async Task Screen_SearchImage_WhenSimilarityIsNotFinite_ReturnsInvalidArguments()
	{
		var reader = new FakeScreenPixelReader();
		var service = new ScreenCliService(reader, new FakeMousePositionProvider());

		foreach (var similarity in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
		{
			var result = await service.ExecuteAsync(new ScreenCliOptions(
				ScreenCliAction.SearchImage,
				ImagePath: "/tmp/template.png",
				Similarity: similarity), CancellationToken.None);

			Assert.False(result.Success);
			Assert.Equal((int)CliExitCode.InvalidArguments, result.ExitCode);
			Assert.Contains("Invalid options", result.Message, StringComparison.Ordinal);
		}
	}

	[Fact]
	public async Task Screen_SearchImage_WhenNoMatch_ReturnsFoundFalseData()
	{
        var imagePath = await WriteTemplatePngAsync();
        try
        {
            var reader = new FakeScreenPixelReader { ImageSearchNoMatch = true };
            var service = new ScreenCliService(reader, new FakeMousePositionProvider());

            var result = await service.ExecuteAsync(new ScreenCliOptions(ScreenCliAction.SearchImage, ImagePath: imagePath), CancellationToken.None);

            Assert.True(result.Success, string.Join("; ", result.Errors));
            var data = Assert.IsType<ScreenSearchImageData>(result.Data);
            Assert.False(data.Found);
            Assert.Null(data.X);
            Assert.Null(data.Y);
            Assert.Null(data.Score);
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    [Fact]
    public async Task Screen_SearchImage_WhenReaderReturnsCanceled_ReturnsCancelledFailure()
    {
        var imagePath = await WriteTemplatePngAsync();
        try
        {
            var reader = new FakeScreenPixelReader
            {
                ImageSearchResult = ScreenReadResult<ScreenImageMatch>.Failure(
                    ScreenReadErrorKind.Canceled,
                    "image search canceled"),
            };
            var service = new ScreenCliService(reader, new FakeMousePositionProvider());

            var result = await service.ExecuteAsync(new ScreenCliOptions(ScreenCliAction.SearchImage, ImagePath: imagePath), CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal((int)CliExitCode.Cancelled, result.ExitCode);
            Assert.Contains("image search canceled", result.Errors);
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    [Fact]
    public async Task Screen_WaitImage_ForwardsRemainingDeadlineToImageSearch()
    {
        var imagePath = await WriteTemplatePngAsync();
        try
        {
            var reader = new FakeScreenPixelReader { ImageSearchNoMatch = true };
            var service = new ScreenCliService(reader, new FakeMousePositionProvider());

            var result = await service.ExecuteAsync(new ScreenCliOptions(
                ScreenCliAction.WaitImage,
                ImagePath: imagePath,
                TimeoutMs: 0), CancellationToken.None);

            Assert.True(result.Success, string.Join("; ", result.Errors));
            Assert.Equal(TimeSpan.Zero, reader.LastImageReadOptions.Timeout);
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    [Fact]
    public async Task Screen_WaitImage_WhenMatchAppears_ReturnsFoundData()
    {
        var imagePath = await WriteTemplatePngAsync();
        try
        {
            var reader = new FakeScreenPixelReader
            {
                ImageMatch = new ScreenImageMatch(new ScreenPoint(9, 10), 0.91),
            };
            var service = new ScreenCliService(reader, new FakeMousePositionProvider());

            var result = await service.ExecuteAsync(new ScreenCliOptions(
                ScreenCliAction.WaitImage,
                ImagePath: imagePath,
                TimeoutMs: 100,
                RegionX: 3,
                RegionY: 4,
                RegionWidth: 50,
                RegionHeight: 60,
                Similarity: 0.85,
                Downsample: 2), CancellationToken.None);

            Assert.True(result.Success, string.Join("; ", result.Errors));
            Assert.Equal(new ScreenRect(3, 4, 50, 60), reader.LastImageRegion);
            Assert.Equal(0.85, reader.LastImageOptions.MinimumSimilarity);
            Assert.Equal(2, reader.LastImageOptions.DownsampleFactor);
            var data = Assert.IsType<ScreenSearchImageData>(result.Data);
            Assert.True(data.Found);
            Assert.Equal(9, data.X);
            Assert.Equal(10, data.Y);
            Assert.Equal(0.91, data.Score);
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    [Fact]
    public async Task Screen_WaitImage_WhenTimeoutExpires_ReturnsFoundFalseData()
    {
        var imagePath = await WriteTemplatePngAsync();
        try
        {
            var reader = new FakeScreenPixelReader { ImageSearchNoMatch = true };
            var service = new ScreenCliService(reader, new FakeMousePositionProvider());

            var result = await service.ExecuteAsync(new ScreenCliOptions(
                ScreenCliAction.WaitImage,
                ImagePath: imagePath,
                TimeoutMs: 0), CancellationToken.None);

            Assert.True(result.Success, string.Join("; ", result.Errors));
            var data = Assert.IsType<ScreenSearchImageData>(result.Data);
            Assert.False(data.Found);
            Assert.Null(data.X);
            Assert.Null(data.Y);
            Assert.Null(data.Score);
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    [Fact]
    public async Task Screen_ImageClick_WhenImageFound_ClicksTemplateCenter()
    {
        var imagePath = await WriteTemplatePngAsync(width: 3, height: 5);
        try
        {
            var reader = new FakeScreenPixelReader
            {
                ImageMatch = new ScreenImageMatch(new ScreenPoint(20, 30), 0.97),
            };
            var input = new FakeInputSimulator();
            reader.ClickInput = input;
            var service = new ScreenCliService(reader, new FakeMousePositionProvider { Resolution = (1920, 1080) });

            var result = await service.ExecuteAsync(new ScreenCliOptions(
                ScreenCliAction.ImageClick,
                ImagePath: imagePath,
                Button: MouseButton.Middle), CancellationToken.None);

            Assert.True(result.Success, string.Join("; ", result.Errors));
            Assert.Equal((1920, 1080), input.InitializedResolution);
            Assert.Equal((21, 32), input.LastAbsoluteMove);
            Assert.Null(input.LastRelativeMove);
            Assert.Equal([MouseButtonCode.Middle, -MouseButtonCode.Middle], input.ButtonEvents);
            Assert.True(input.Synced);
            var data = Assert.IsType<ScreenImageClickData>(result.Data);
            Assert.Equal(21, data.X);
            Assert.Equal(32, data.Y);
            Assert.Equal("Middle", data.Button);
            Assert.Equal(0.97, data.Score);
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    [Fact]
    public async Task Screen_ImageClick_WhenAbsoluteCoordinatesAreUnsupported_UsesRelativeMovement()
    {
        var imagePath = await WriteTemplatePngAsync(width: 3, height: 5);
        try
        {
            var reader = new FakeScreenPixelReader
            {
                ImageMatch = new ScreenImageMatch(new ScreenPoint(20, 30), 0.97),
            };
            var input = new FakeInputSimulator { SupportsAbsoluteCoordinates = false };
            reader.ClickPosition = (10, 20);
            reader.ClickInput = input;
            var service = new ScreenCliService(reader, new FakeMousePositionProvider { Position = (10, 20) });

            var result = await service.ExecuteAsync(new ScreenCliOptions(
                ScreenCliAction.ImageClick,
                ImagePath: imagePath), CancellationToken.None);

            Assert.True(result.Success, string.Join("; ", result.Errors));
            Assert.Null(input.LastAbsoluteMove);
            Assert.Equal((11, 12), input.LastRelativeMove);
            Assert.Equal([MouseButtonCode.Left, -MouseButtonCode.Left], input.ButtonEvents);
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Screen_ImageClick_WhenRelativeMovementCannotResolvePosition_ReturnsEnvironmentError(bool providerSupported, bool positionAvailable)
    {
        var imagePath = await WriteTemplatePngAsync();
        try
        {
            var reader = new FakeScreenPixelReader();
            var input = new FakeInputSimulator { SupportsAbsoluteCoordinates = false };
            reader.ClickPositionSupported = providerSupported;
            reader.ClickPosition = positionAvailable ? (10, 20) : null;
            reader.ClickInput = input;
            var service = new ScreenCliService(reader, new FakeMousePositionProvider { IsSupported = providerSupported, Position = positionAvailable ? (10, 20) : null });

            var result = await service.ExecuteAsync(new ScreenCliOptions(
                ScreenCliAction.ImageClick,
                ImagePath: imagePath), CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal((int)CliExitCode.EnvironmentError, result.ExitCode);
            Assert.Empty(input.ButtonEvents);
            Assert.Contains("requires absolute coordinate support", result.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    [Fact]
    public async Task Screen_SearchImage_WhenFileMissing_ReturnsInvalidArguments()
    {
        var service = new ScreenCliService(new FakeScreenPixelReader(), new FakeMousePositionProvider());

        var result = await service.ExecuteAsync(new ScreenCliOptions(ScreenCliAction.SearchImage, ImagePath: Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.png")), CancellationToken.None);

        AssertInvalidArguments(result);
        Assert.Contains("not found", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Screen_SearchImage_WhenTemplateExceedsSupportedDimensions_ReturnsInvalidArguments()
    {
        var imagePath = Path.Combine(Path.GetTempPath(), $"crossmacro-template-{Guid.NewGuid():N}.png");
        await File.WriteAllBytesAsync(imagePath, CreateOversizedPngBytes());
        try
        {
            var service = new ScreenCliService(new FakeScreenPixelReader(), new FakeMousePositionProvider());

            var result = await service.ExecuteAsync(new ScreenCliOptions(ScreenCliAction.SearchImage, ImagePath: imagePath), CancellationToken.None);

            AssertInvalidArguments(result);
            Assert.Contains("not a supported PNG", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("maximum supported size of 7680x4320", result.Errors[0], StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    [Fact]
    public async Task Screen_UnsupportedReader_ReturnsEnvironmentError()
    {
        var service = new ScreenCliService(new FakeScreenPixelReader { IsSupported = false }, new FakeMousePositionProvider());

        var result = await service.ExecuteAsync(new ScreenCliOptions(ScreenCliAction.Pixel, 1, 2), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal((int)CliExitCode.EnvironmentError, result.ExitCode);
    }

    [Fact]
    public async Task Screenshot_Capture_WritesPngAndReturnsData()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"crossmacro-shot-{Guid.NewGuid():N}.png");
        try
        {
            var capture = new FakeScreenshotCaptureService();
            var service = new ScreenshotCliService(capture);

            var result = await service.ExecuteAsync(new ScreenshotCliOptions(
                ScreenshotCliAction.Capture,
                outputPath,
                RegionX: 1,
                RegionY: 2,
                RegionWidth: 2,
                RegionHeight: 1), CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(new ScreenRect(1, 2, 2, 1), capture.LastRegion);
            Assert.True(File.Exists(outputPath));
            var bytes = await File.ReadAllBytesAsync(outputPath);
            Assert.Equal([0x89, 0x50, 0x4E, 0x47], bytes[..4]);
            var data = Assert.IsType<ScreenshotData>(result.Data);
            Assert.Equal(Path.GetFullPath(outputPath), data.OutputPath);
            Assert.Equal(2, data.Width);
            Assert.Equal(1, data.Height);
            Assert.Equal("png", data.Format);
            Assert.True(data.IsRegion);
            Assert.False(data.CopiedToClipboard);
        }
        finally
        {
            File.Delete(outputPath);
        }
    }

    [Fact]
    public async Task Screenshot_UnsupportedProvider_ReturnsEnvironmentError()
    {
        var service = new ScreenshotCliService(new FakeScreenshotCaptureService { IsSupported = false });

        var result = await service.ExecuteAsync(new ScreenshotCliOptions(ScreenshotCliAction.Capture, "shot.png"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal((int)CliExitCode.EnvironmentError, result.ExitCode);
    }

    [Fact]
    public async Task Screenshot_Clipboard_CopiesPngAndReturnsData()
    {
        var capture = new FakeScreenshotCaptureService();
        var service = new ScreenshotCliService(capture);

        var result = await service.ExecuteAsync(new ScreenshotCliOptions(ScreenshotCliAction.Capture, Clipboard: true), CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(capture.PngBytes);
        Assert.Equal([0x89, 0x50, 0x4E, 0x47], capture.PngBytes[..4]);
        var data = Assert.IsType<ScreenshotData>(result.Data);
        Assert.Null(data.OutputPath);
        Assert.True(data.CopiedToClipboard);
    }

    [Fact]
    public async Task Screenshot_ClipboardUnsupported_ReturnsEnvironmentError()
    {
        var service = new ScreenshotCliService(
            new FakeScreenshotCaptureService { ClipboardSupported = false });

        var result = await service.ExecuteAsync(new ScreenshotCliOptions(ScreenshotCliAction.Capture, Clipboard: true), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal((int)CliExitCode.EnvironmentError, result.ExitCode);
    }

    [Fact]
    public async Task Screenshot_WhenImageClipboardToolIsMissing_ReturnsEnvironmentError()
    {
        var service = new ScreenshotCliService(
            new FakeScreenshotCaptureService { ClipboardSupported = false });

        var result = await service.ExecuteAsync(new ScreenshotCliOptions(ScreenshotCliAction.Capture, Clipboard: true), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal((int)CliExitCode.EnvironmentError, result.ExitCode);
    }

    [Fact]
    public async Task Screenshot_WhenImageClipboardWriteFails_ReturnsRuntimeError()
    {
        var service = new ScreenshotCliService(
            new FakeScreenshotCaptureService { ThrowClipboardWriteFailure = true });

        var result = await service.ExecuteAsync(new ScreenshotCliOptions(ScreenshotCliAction.Capture, Clipboard: true), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal((int)CliExitCode.RuntimeError, result.ExitCode);
    }

    [Fact]
    public async Task Screenshot_WhenOutputCannotBeWritten_ReturnsFileError()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), $"crossmacro-shot-dir-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directoryPath);
        try
        {
            var service = new ScreenshotCliService(new FakeScreenshotCaptureService());

            var result = await service.ExecuteAsync(new ScreenshotCliOptions(ScreenshotCliAction.Capture, directoryPath), CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal((int)CliExitCode.FileError, result.ExitCode);
        }
        finally
        {
            Directory.Delete(directoryPath);
        }
    }

    [Fact]
    public async Task Screenshot_WhenDestinationMissing_ReturnsInvalidArgumentsBeforeCapture()
    {
        var capture = new FakeScreenshotCaptureService();
        var service = new ScreenshotCliService(capture);

        var result = await service.ExecuteAsync(new ScreenshotCliOptions(ScreenshotCliAction.Capture), CancellationToken.None);

        AssertInvalidArguments(result);
        Assert.Equal(0, capture.CaptureCalls);
    }

    [Fact]
    public async Task Screenshot_WhenRegionIsPartial_ReturnsInvalidArgumentsBeforeCapture()
    {
        var capture = new FakeScreenshotCaptureService();
        var service = new ScreenshotCliService(capture);

        var result = await service.ExecuteAsync(new ScreenshotCliOptions(
            ScreenshotCliAction.Capture,
            "shot.png",
            RegionX: 1,
            RegionY: 2), CancellationToken.None);

        AssertInvalidArguments(result);
        Assert.Equal(0, capture.CaptureCalls);
    }

    [Fact]
    public async Task Screenshot_WhenRegionSizeIsInvalid_ReturnsInvalidArgumentsBeforeCapture()
    {
        var capture = new FakeScreenshotCaptureService();
        var service = new ScreenshotCliService(capture);

        var result = await service.ExecuteAsync(new ScreenshotCliOptions(
            ScreenshotCliAction.Capture,
            "shot.png",
            RegionX: 1,
            RegionY: 2,
            RegionWidth: 0,
            RegionHeight: 1), CancellationToken.None);

        AssertInvalidArguments(result);
        Assert.Equal(0, capture.CaptureCalls);
    }

    [Fact]
    public async Task Screen_InvalidActionOptions_ReturnInvalidArguments()
    {
        var service = new ScreenCliService(new FakeScreenPixelReader(), new FakeMousePositionProvider());

        var missingWaitColor = await service.ExecuteAsync(new ScreenCliOptions(ScreenCliAction.WaitColor, 1, 2), CancellationToken.None);
        var missingSearchBounds = await service.ExecuteAsync(new ScreenCliOptions(ScreenCliAction.SearchColor, 0, 0, new ScreenPixelColor(255, 0, 0)), CancellationToken.None);
        var zeroWidthSearch = await service.ExecuteAsync(new ScreenCliOptions(ScreenCliAction.SearchColor, 0, 0, new ScreenPixelColor(255, 0, 0), X2: 0, Y2: 10), CancellationToken.None);

        AssertInvalidArguments(missingWaitColor);
        AssertInvalidArguments(missingSearchBounds);
        AssertInvalidArguments(zeroWidthSearch);
    }

    private static void AssertInvalidArguments(CliCommandExecutionResult result)
    {
        Assert.False(result.Success);
        Assert.Equal((int)CliExitCode.InvalidArguments, result.ExitCode);
    }

    private static async Task<string> WriteTemplatePngAsync(int width = 1, int height = 1)
    {
        var path = Path.Combine(Path.GetTempPath(), $"crossmacro-template-{Guid.NewGuid():N}.png");
        var pixels = new byte[checked(width * height * 3)];
        Array.Fill<byte>(pixels, 0x56);
        using var frame = new ScreenFrame(
            new ScreenRect(0, 0, width, height),
            width * 3,
            ScreenPixelFormat.Rgb24,
            pixels);
        await using var output = File.Create(path);
        ScreenFramePngEncoder.Encode(frame, output);
        return path;
    }

    private static byte[] CreateOversizedPngBytes()
    {
        return
        [
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x0D,
            0x49, 0x48, 0x44, 0x52,
            0x00, 0x00, 0x1E, 0x01,
            0x00, 0x00, 0x00, 0x01,
            0x08,
            0x02,
            0x00,
            0x00,
            0x00,
            0x6C, 0xF7, 0xBC, 0x13,
        ];
    }

    private sealed class FakeClipboardService : IClipboardService
    {
        public bool IsSupported { get; init; } = true;
        public string? Text { get; set; }
        public Task SetTextAsync(string text, CancellationToken cancellationToken = default)
        {
            Text = text;
            return Task.CompletedTask;
        }

        public Task<string?> GetTextAsync(CancellationToken cancellationToken = default) => Task.FromResult(Text);
    }

    private sealed class FakeWindowManager : IWindowManager
    {
        public IReadOnlyList<WindowInfo> Windows { get; init; } = [];
        public string? FocusedClass { get; private set; }
        public Task<WindowInfo?> GetActiveWindowAsync(CancellationToken cancellationToken = default) => Task.FromResult(Windows.FirstOrDefault());
        public Task<IReadOnlyList<WindowInfo>> GetWindowsAsync(CancellationToken cancellationToken = default) => Task.FromResult(Windows);
        public Task<bool> FocusWindowByAddressAsync(string address, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> FocusWindowByTitleAsync(string titleSubstring, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> FocusWindowByClassAsync(string classSubstring, CancellationToken cancellationToken = default)
        {
            FocusedClass = classSubstring;
            return Task.FromResult(true);
        }

        public Task<bool> CloseWindowByAddressAsync(string address, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> CloseWindowByTitleAsync(string titleSubstring, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> MoveActiveWindowAsync(int x, int y, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> ResizeActiveWindowAsync(int width, int height, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> FullscreenActiveWindowAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> MaximizeActiveWindowAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> FloatActiveWindowAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> CenterActiveWindowAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<string?> GetActiveWorkspaceAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>("1");
        public Task<bool> SwitchWorkspaceAsync(string workspace, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> MoveActiveWindowToWorkspaceAsync(string workspace, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> MoveWindowToWorkspaceByAddressAsync(string address, string workspace, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class FakeScreenPixelReader : IScreenPixelReader, IScreenImageSearchReader, IScreenImageAutomation
    {
        public string ProviderName => "fake-screen";
        public bool IsSupported { get; init; } = true;
        public ScreenPoint LastPoint { get; private set; }
        public ScreenRect LastRegion { get; private set; }
        public ScreenRect? LastImageRegion { get; private set; }
        public ScreenFrame? LastImageTemplate { get; private set; }
        public ScreenImageMatchOptions LastImageOptions { get; private set; } = ScreenImageMatchOptions.Default;
        public ScreenReadOptions LastImageReadOptions { get; private set; }
        public int LastTolerance { get; private set; }
        public ScreenReadOptions LastPixelOptions { get; private set; }
        public ScreenReadOptions LastSearchOptions { get; private set; }
        public ScreenReadOptions LastWaitOptions { get; private set; }
        public ScreenImageMatch ImageMatch { get; init; } = new(new ScreenPoint(1, 1), 1.0);
        public bool ImageSearchNoMatch { get; init; }
        public ScreenReadResult<ScreenImageMatch>? ImageSearchResult { get; init; }
        public IInputSimulator? ClickInput { get; set; }
        public bool ClickPositionSupported { get; set; } = true;
        public (int X, int Y)? ClickPosition { get; set; } = (0, 0);
        string IScreenImageAutomation.ProviderName => ProviderName;
        bool IScreenImageAutomation.IsSupported => IsSupported;

        public async Task<ScreenImageAutomationResult> SearchAsync(ScreenImageAutomationRequest request, CancellationToken cancellationToken)
        {
            ScreenFrame template;
            try
            {
                template = await new ImageAssetCodec().DecodeFileAsync(request.ImagePath, cancellationToken);
            }
            catch (FileNotFoundException ex)
            {
                return ScreenImageAutomationResult.Failure(ScreenReadErrorKind.InvalidArguments, $"Image file was not found: {ex.Message}");
            }
            catch (InvalidDataException ex)
            {
                return ScreenImageAutomationResult.Failure(ScreenReadErrorKind.InvalidArguments, $"Image file is not a supported PNG: {ex.Message}");
            }
            var result = await SearchImageAsync(request.Region, template, ScreenImageMatchOptions.Create(request.Region, request.Similarity, request.Downsample, request.MatchMode is ScreenImageMatchMode.Best ? ScreenImageMatchSelectionMode.BestMatch : ScreenImageMatchSelectionMode.FirstThresholdMatch, request.ScaleAware), new ScreenReadOptions(request.Timeout, ScreenReadOptions.Default.PollInterval, cancellationToken));
            return result.IsSuccess ? ScreenImageAutomationResult.FoundAt(result.Value.Point, result.Value.Score) : ScreenImageAutomationResult.Failure(result.ErrorKind!.Value, result.ErrorMessage!);
        }

        public Task<ScreenImageAutomationResult> WaitAsync(ScreenImageAutomationRequest request, CancellationToken cancellationToken) => SearchAsync(request, cancellationToken);

        public async Task<ScreenImageAutomationResult> ClickAsync(ScreenImageAutomationRequest request, int buttonCode, CancellationToken cancellationToken)
        {
            var result = await SearchAsync(request, cancellationToken);
            if (!result.IsSuccess || ClickInput is null) return result;
            ClickInput.Initialize(1920, 1080);
            if (ClickInput is IInputSimulatorCapabilities { SupportsAbsoluteCoordinates: false })
            {
                if (!ClickPositionSupported || ClickPosition is not { } position)
                {
                    return ScreenImageAutomationResult.Failure(ScreenReadErrorKind.Unsupported, "No supported IMousePositionProvider is available for relative movement.");
                }
                ClickInput.MoveRelative(result.Point!.Value.X + 1 - position.X, result.Point.Value.Y + 2 - position.Y);
            }
            else
            {
                ClickInput.MoveAbsolute(result.Point!.Value.X + 1, result.Point.Value.Y + 2);
            }
            ClickInput.MouseButton(buttonCode, pressed: true);
            ClickInput.MouseButton(buttonCode, pressed: false);
            ClickInput.Sync();
            return ScreenImageAutomationResult.FoundAt(new ScreenPoint(result.Point.Value.X + 1, result.Point.Value.Y + 2), result.Score!.Value);
        }
        public Task<ScreenReadResult<ScreenPixelColor>> GetPixelAsync(ScreenPoint point, ScreenReadOptions options)
        {
            LastPoint = point;
            LastPixelOptions = options;
            return Task.FromResult(ScreenReadResult<ScreenPixelColor>.Success(new ScreenPixelColor(0x12, 0x34, 0x56)));
        }

        public Task<ScreenReadResult<ScreenPixelColor>> WaitForPixelAsync(ScreenPoint point, ScreenPixelColor expected, ScreenReadOptions options)
        {
            LastWaitOptions = options;
            return Task.FromResult(ScreenReadResult<ScreenPixelColor>.Success(expected));
        }

        public Task<ScreenReadResult<ScreenPixelSearchMatch>> SearchPixelAsync(ScreenRect region, ScreenPixelColor expected, int tolerance, ScreenReadOptions options)
        {
            LastRegion = region;
            LastTolerance = tolerance;
            LastSearchOptions = options;
            return Task.FromResult(ScreenReadResult<ScreenPixelSearchMatch>.Success(new ScreenPixelSearchMatch(new ScreenPoint(region.X + 1, region.Y + 1), expected)));
        }

        public Task<ScreenReadResult<ScreenImageMatch>> SearchImageAsync(
            ScreenRect? region,
            ScreenFrame template,
            ScreenImageMatchOptions options,
            ScreenReadOptions readOptions)
        {
            LastImageRegion = region;
            LastImageTemplate = template;
            LastImageOptions = options;
            LastImageReadOptions = readOptions;
            if (ImageSearchResult is { } configuredResult)
            {
                return Task.FromResult(configuredResult);
            }

            return ImageSearchNoMatch
                ? Task.FromResult(ScreenReadResult<ScreenImageMatch>.Failure(ScreenReadErrorKind.CaptureTimeout, "No image matching the template was found."))
                : Task.FromResult(ScreenReadResult<ScreenImageMatch>.Success(ImageMatch));
        }

        public void Dispose()
        {
        }
    }

    private sealed class FakeScreenshotCaptureService : IScreenshotCaptureService
    {
        public bool IsSupported { get; init; } = true;
        public bool ClipboardSupported { get; init; } = true;
        public bool ThrowClipboardWriteFailure { get; init; }
        public ScreenRect? LastRegion { get; private set; }
        public int CaptureCalls { get; private set; }
        public byte[]? PngBytes { get; private set; }

        public async Task<ScreenshotCaptureResult> CaptureAsync(
            string? outputPath,
            bool copyToClipboard,
            ScreenRect? region,
            CancellationToken cancellationToken)
        {
            CaptureCalls++;
            LastRegion = region;
            if (!IsSupported)
            {
                return ScreenshotCaptureResult.Fail(ScreenshotCaptureFailureKind.ProviderUnsupported, "Screenshot provider is not supported.", []);
            }

            if (copyToClipboard && !ClipboardSupported)
            {
                return ScreenshotCaptureResult.Fail(ScreenshotCaptureFailureKind.ClipboardUnsupported, "Image clipboard is not supported.", []);
            }

            if (ThrowClipboardWriteFailure)
            {
                return ScreenshotCaptureResult.Fail(ScreenshotCaptureFailureKind.ClipboardWriteFailed, "Clipboard write failed.", []);
            }

            if (outputPath is not null)
            {
                try
                {
                    await File.WriteAllBytesAsync(outputPath, [0x89, 0x50, 0x4E, 0x47], cancellationToken);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    return ScreenshotCaptureResult.Fail(ScreenshotCaptureFailureKind.FileWriteFailed, "Screenshot output could not be written.", [ex.Message]);
                }
            }

            if (copyToClipboard)
            {
                PngBytes = [0x89, 0x50, 0x4E, 0x47];
            }

            var bounds = region ?? new ScreenRect(0, 0, 2, 1);
            return ScreenshotCaptureResult.Ok(new ScreenshotCaptureData(
                outputPath is null ? null : Path.GetFullPath(outputPath),
                bounds.Width,
                bounds.Height,
                "png",
                "fake-frame",
                region.HasValue,
                copyToClipboard));
        }
    }

    private sealed class FakeInputSimulator : IInputSimulator, IInputSimulatorCapabilities
    {
        public string ProviderName => "fake-input";
        public bool IsSupported { get; init; } = true;
        public bool SupportsAbsoluteCoordinates { get; init; } = true;
        public (int Width, int Height) InitializedResolution { get; private set; }
        public (int X, int Y)? LastAbsoluteMove { get; private set; }
        public (int X, int Y)? LastRelativeMove { get; private set; }
        public List<int> ButtonEvents { get; } = [];
        public bool Synced { get; private set; }

        public void Initialize(int screenWidth = 0, int screenHeight = 0)
        {
            InitializedResolution = (screenWidth, screenHeight);
        }

        public void MoveAbsolute(int x, int y)
        {
            LastAbsoluteMove = (x, y);
        }

        public void MoveRelative(int dx, int dy)
        {
            LastRelativeMove = (dx, dy);
        }

        public void MouseButton(int button, bool pressed)
        {
            ButtonEvents.Add(pressed ? button : -button);
        }

        public void Scroll(int delta, bool isHorizontal = false)
        {
        }

        public void KeyPress(int keyCode, bool pressed)
        {
        }

        public void Sync()
        {
            Synced = true;
        }

        public void Dispose()
        {
        }
    }

    private sealed class FakeMousePositionProvider : IMousePositionProvider
    {
        public string ProviderName => "fake-mouse";
        public bool IsSupported { get; init; } = true;
        public (int X, int Y)? Position { get; init; } = (0, 0);
        public (int Width, int Height)? Resolution { get; init; } = (1920, 1080);
        public Task<(int X, int Y)?> GetAbsolutePositionAsync() => Task.FromResult(Position);
        public Task<(int Width, int Height)?> GetScreenResolutionAsync() => Task.FromResult(Resolution);
        public void Dispose()
        {
        }
    }
}
