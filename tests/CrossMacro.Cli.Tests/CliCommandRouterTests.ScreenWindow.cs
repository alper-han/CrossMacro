namespace CrossMacro.Cli.Tests;

public sealed partial class CliCommandRouterTests
{

    [Fact]
    public void Parse_WhenRunWithInlineScreenReadingSteps_ReturnsRunOptions()
    {
        var result = CliCommandRouterAccessor.Parse([
            "run",
            "pixelcolor", "1", "2", "sampled",
            "waitcolor", "3", "4", "00FF00", "100", "wait_ok",
            "pixelsearch", "0", "0", "10", "10", "FF0000", "found", "found_x", "found_y", "tolerance", "26",
            "click", "left",
        ]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<RunCliOptions>(result.Options);
        Assert.Equal("pixelcolor 1 2 sampled", options.Steps[0]);
        Assert.Equal("waitcolor 3 4 00FF00 100 wait_ok", options.Steps[1]);
        Assert.Equal("pixelsearch 0 0 10 10 FF0000 found found_x found_y tolerance 26", options.Steps[2]);
        Assert.Equal("click left", options.Steps[3]);
    }

    [Fact]
    public void Parse_WhenRunWithInlineScreenReadingOptionalForms_ReturnsRunOptions()
    {
        var result = CliCommandRouterAccessor.Parse([
            "run",
            "pixelcolor", "rel", "-1", "2",
            "pixelcolor", "rel", "-3", "4", "relativeSampled",
            "waitcolor", "3", "4", "00FF00",
            "pixelsearch", "0", "0", "10", "10", "FF0000", "tolerance", "26",
            "pixelsearch", "1", "2", "11", "12", "00FF00", "found_x", "found_y", "tolerance", "7",
            "click", "left",
        ]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<RunCliOptions>(result.Options);
        Assert.Equal("pixelcolor rel -1 2", options.Steps[0]);
        Assert.Equal("pixelcolor rel -3 4 relativeSampled", options.Steps[1]);
        Assert.Equal("waitcolor 3 4 00FF00", options.Steps[2]);
        Assert.Equal("pixelsearch 0 0 10 10 FF0000 tolerance 26", options.Steps[3]);
        Assert.Equal("pixelsearch 1 2 11 12 00FF00 found_x found_y tolerance 7", options.Steps[4]);
        Assert.Equal("click left", options.Steps[5]);
    }

    [Fact]
    public void Parse_WhenRunWithInlineMalformedScreenReadingPrefix_ReturnsError()
    {
        var result = CliCommandRouterAccessor.Parse(["run", "pixelcolorful", "1", "2", "sampled"]);

        Assert.False(result.IsSuccess);
        Assert.Contains("Unknown inline run step command: pixelcolorful", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void GetUsage_WhenRunHelp_IncludesScreenReadingSteps()
    {
        var usage = CliCommandRouterAccessor.GetUsage("run");

        Assert.Contains("pixelcolor <x> <y> [var]", usage, StringComparison.Ordinal);
        Assert.Contains("pixelcolor rel <dx> <dy> [var]", usage, StringComparison.Ordinal);
        Assert.Contains("waitcolor <x> <y> <RRGGBB|$var> [timeout_ms] [result_var]", usage, StringComparison.Ordinal);
        Assert.Contains("pixelsearch <x1> <y1> <x2> <y2> <RRGGBB|$var> [found_var var_x var_y|var_x var_y] [timeout <ms>] [tolerance <0..255>]", usage, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_WhenWindowCommands_ReturnTypedOptions()
    {
        var search = CliCommandRouterAccessor.Parse(["window", "search", "--title", "Firefox", "--json"]);
        var wait = CliCommandRouterAccessor.Parse(["window", "wait", "--class", "Code", "--timeout-ms", "1500"]);
        var move = CliCommandRouterAccessor.Parse(["window", "move", "--active", "10", "20"]);
        var workspace = CliCommandRouterAccessor.Parse(["window", "workspace", "move-window", "--address", "0xabc", "dev"]);

        Assert.True(search.IsSuccess);
        Assert.Equal(WindowCliAction.Search, Assert.IsType<WindowCliOptions>(search.Options).Action);
        Assert.True(Assert.IsType<WindowCliOptions>(search.Options).JsonOutput);
        Assert.True(wait.IsSuccess);
        Assert.Equal(1500, Assert.IsType<WindowCliOptions>(wait.Options).TimeoutMs);
        Assert.True(move.IsSuccess);
        Assert.Equal(10, Assert.IsType<WindowCliOptions>(move.Options).X);
        Assert.True(workspace.IsSuccess);
        var workspaceOptions = Assert.IsType<WindowCliOptions>(workspace.Options);
        Assert.Equal(WindowCliAction.WorkspaceMoveWindow, workspaceOptions.Action);
        Assert.Equal("dev", workspaceOptions.WorkspaceName);
    }

    [Fact]
    public void Parse_WhenWindowFocusHasMultipleSelectors_ReturnsError()
    {
        var result = CliCommandRouterAccessor.Parse(["window", "focus", "--title", "A", "--class", "B"]);

        Assert.False(result.IsSuccess);
        Assert.Contains("Only one window selector", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_WhenScreenCommands_ReturnTypedOptions()
    {
        var pixel = CliCommandRouterAccessor.Parse(["screen", "pixel", "--relative", "-1", "2", "--json"]);
        var wait = CliCommandRouterAccessor.Parse(["screen", "wait-color", "3", "4", "00ff00", "--timeout-ms", "500"]);
        var search = CliCommandRouterAccessor.Parse(["screen", "search-color", "0", "0", "10", "20", "FF0000", "--timeout-ms", "450", "--tolerance", "26"]);
        var imageSearch = CliCommandRouterAccessor.Parse(["screen", "search-image", "/tmp/template.png", "--region", "1", "2", "30", "40", "--similarity", "0.9", "--json"]);
        var firstImageSearch = CliCommandRouterAccessor.Parse(["screen", "search-image", "/tmp/template.png", "--matchmode", "first"]);
        var bestImageSearch = CliCommandRouterAccessor.Parse(["screen", "search-image", "/tmp/template.png", "--matchmode", "best"]);
        var automaticImageSearch = CliCommandRouterAccessor.Parse(["screen", "search-image", "/tmp/template.png"]);
        var waitImage = CliCommandRouterAccessor.Parse(["screen", "wait-image", "/tmp/template.png", "--timeout-ms", "750", "--region", "2", "3", "40", "50", "--similarity", "0.8"]);
        var imageClick = CliCommandRouterAccessor.Parse(["screen", "image-click", "/tmp/template.png", "--timeout-ms", "650", "--button", "right", "--region", "4", "5", "60", "70", "--similarity", "0.7"]);

        Assert.True(pixel.IsSuccess);
        var pixelOptions = Assert.IsType<ScreenCliOptions>(pixel.Options);
        Assert.True(pixelOptions.Relative);
        Assert.Equal(-1, pixelOptions.X);
        Assert.Null(pixelOptions.TimeoutMs);
        Assert.True(pixelOptions.JsonOutput);
        Assert.True(wait.IsSuccess);
        var waitOptions = Assert.IsType<ScreenCliOptions>(wait.Options);
        Assert.Equal(500, waitOptions.TimeoutMs);
        Assert.True(search.IsSuccess);
        var searchOptions = Assert.IsType<ScreenCliOptions>(search.Options);
        Assert.Equal(26, searchOptions.Tolerance);
        Assert.Equal(450, searchOptions.TimeoutMs);
        Assert.True(imageSearch.IsSuccess);
        var imageSearchOptions = Assert.IsType<ScreenCliOptions>(imageSearch.Options);
        Assert.Equal(ScreenCliAction.SearchImage, imageSearchOptions.Action);
        Assert.Equal("/tmp/template.png", imageSearchOptions.ImagePath);
        Assert.Equal(1, imageSearchOptions.RegionX);
        Assert.Equal(2, imageSearchOptions.RegionY);
        Assert.Equal(30, imageSearchOptions.RegionWidth);
        Assert.Equal(40, imageSearchOptions.RegionHeight);
        Assert.Equal(0.9, imageSearchOptions.Similarity);
        Assert.Null(imageSearchOptions.TimeoutMs);
        Assert.True(imageSearchOptions.JsonOutput);
        Assert.Equal(ScreenImageMatchMode.Automatic, imageSearchOptions.MatchMode);
        Assert.True(firstImageSearch.IsSuccess);
        Assert.Equal(ScreenImageMatchMode.First, Assert.IsType<ScreenCliOptions>(firstImageSearch.Options).MatchMode);
        Assert.True(bestImageSearch.IsSuccess);
        Assert.Equal(ScreenImageMatchMode.Best, Assert.IsType<ScreenCliOptions>(bestImageSearch.Options).MatchMode);
        Assert.True(automaticImageSearch.IsSuccess);
        var automaticImageSearchOptions = Assert.IsType<ScreenCliOptions>(automaticImageSearch.Options);
        Assert.Equal(ScreenImageMatchMode.Automatic, automaticImageSearchOptions.MatchMode);
        Assert.Equal(0.95, automaticImageSearchOptions.Similarity);
        Assert.True(waitImage.IsSuccess);
        var waitImageOptions = Assert.IsType<ScreenCliOptions>(waitImage.Options);
        Assert.Equal(ScreenCliAction.WaitImage, waitImageOptions.Action);
        Assert.Equal(750, waitImageOptions.TimeoutMs);
        Assert.Equal(2, waitImageOptions.RegionX);
        Assert.Equal(3, waitImageOptions.RegionY);
        Assert.Equal(40, waitImageOptions.RegionWidth);
        Assert.Equal(50, waitImageOptions.RegionHeight);
        Assert.Equal(0.8, waitImageOptions.Similarity);
        Assert.True(imageClick.IsSuccess);
        var imageClickOptions = Assert.IsType<ScreenCliOptions>(imageClick.Options);
        Assert.Equal(ScreenCliAction.ImageClick, imageClickOptions.Action);
        Assert.Equal(MacroMouseButton.Right, imageClickOptions.Button);
        Assert.Equal(4, imageClickOptions.RegionX);
        Assert.Equal(5, imageClickOptions.RegionY);
        Assert.Equal(60, imageClickOptions.RegionWidth);
        Assert.Equal(70, imageClickOptions.RegionHeight);
        Assert.Equal(0.7, imageClickOptions.Similarity);
        Assert.Equal(650, imageClickOptions.TimeoutMs);
    }

    [Fact]
    public void Parse_WhenTopLevelInputCommands_ReturnInputOptions()
    {
        var move = CliCommandRouterAccessor.Parse(["move", "abs", "10", "20", "--dry-run"]);
        var click = CliCommandRouterAccessor.Parse(["click", "current", "right", "--json"]);
        var type = CliCommandRouterAccessor.Parse(["type", "hello world", "--dry-run"]);
        var preciseDelay = CliCommandRouterAccessor.Parse(["delay", "2.375ms"]);
        var delay = CliCommandRouterAccessor.Parse(["delay", "random", "10", "20"]);

        Assert.True(move.IsSuccess);
        var moveOptions = Assert.IsType<InputCliOptions>(move.Options);
        Assert.Equal("move abs 10 20", moveOptions.Step);
        Assert.True(moveOptions.DryRun);
        Assert.True(click.IsSuccess);
        Assert.Equal("click current right", Assert.IsType<InputCliOptions>(click.Options).Step);
        Assert.True(Assert.IsType<InputCliOptions>(click.Options).JsonOutput);
        Assert.True(type.IsSuccess);
        Assert.Equal("type hello world", Assert.IsType<InputCliOptions>(type.Options).Step);
        Assert.True(preciseDelay.IsSuccess);
        Assert.Equal("delay 2.375ms", Assert.IsType<InputCliOptions>(preciseDelay.Options).Step);
        Assert.True(delay.IsSuccess);
        Assert.Equal("delay random 10 20", Assert.IsType<InputCliOptions>(delay.Options).Step);
    }

    [Fact]
    public void Parse_WhenRunHasImageAssetsAndPixelSearchTimeout_PreservesBoth()
    {
        var result = CliCommandRouterAccessor.Parse([
            "run",
            "--asset", "button", "/tmp/button.png",
            "pixelsearch", "0", "0", "10", "10", "FF0000", "found", "x", "y", "timeout", "5000", "tolerance", "5",
            "--dry-run",
        ]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<RunCliOptions>(result.Options);
        Assert.True(options.DryRun);
        Assert.Equal("/tmp/button.png", Assert.Single(options.ImageAssets!).FilePath);
        Assert.Equal("button", options.ImageAssets![0].Name);
        Assert.Equal("pixelsearch 0 0 10 10 FF0000 found x y timeout 5000 tolerance 5", Assert.Single(options.Steps));
    }

    [Fact]
    public void Parse_WhenRunInlineRuntimeStepHasNegativeOperands_PreservesTheStep()
    {
        var result = CliCommandRouterAccessor.Parse(["run", "window", "move", "-10", "-20", "click", "left"]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<RunCliOptions>(result.Options);
        Assert.Equal(["window move -10 -20", "click left"], options.Steps);
    }

    [Fact]
    public void Parse_WhenRunImageAssetNameIsMalformed_ReturnsError()
    {
        var result = CliCommandRouterAccessor.Parse(["run", "--asset", "bad-name", "/tmp/button.png", "--step", "click left"]);

        Assert.False(result.IsSuccess);
        Assert.Contains("Invalid run image asset name", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_WhenScreenPollingOptionsAreUsed_ReturnsErrors()
    {
        var color = CliCommandRouterAccessor.Parse(["screen", "search-color", "0", "0", "10", "10", "FF0000", "--timeout-ms", "5000", "--poll-ms", "25"]);
        var image = CliCommandRouterAccessor.Parse(["screen", "search-image", "/tmp/button.png", "--poll"]);

        Assert.False(color.IsSuccess);
        Assert.Contains("Unknown option for screen search-color: --poll-ms", color.ErrorMessage, StringComparison.Ordinal);
        Assert.False(image.IsSuccess);
        Assert.Contains("Unknown option for screen search-image: --poll", image.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_WhenScreenSearchToleranceOutOfRange_ReturnsError()
    {
        var result = CliCommandRouterAccessor.Parse(["screen", "search-color", "0", "0", "10", "20", "FF0000", "--tolerance", "256"]);

        Assert.False(result.IsSuccess);
        Assert.Contains("--tolerance", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_WhenScreenSearchImageMissingPath_ReturnsError()
    {
        var result = CliCommandRouterAccessor.Parse(["screen", "search-image", "--json"]);

        Assert.False(result.IsSuccess);
        Assert.Equal("screen search-image requires <image-path>.", result.ErrorMessage);
        Assert.True(result.PrefersJsonOutput);
    }

    [Fact]
    public void Parse_WhenScreenSearchImageInvalidOptions_ReturnsError()
    {
        var badSimilarity = CliCommandRouterAccessor.Parse(["screen", "search-image", "/tmp/template.png", "--similarity", "1.1"]);
        var badSimilarityNaN = CliCommandRouterAccessor.Parse(["screen", "search-image", "/tmp/template.png", "--similarity", "NaN"]);
        var badSimilarityInfinity = CliCommandRouterAccessor.Parse(["screen", "search-image", "/tmp/template.png", "--similarity", "Infinity"]);
        var badSimilarityNegativeInfinity = CliCommandRouterAccessor.Parse(["screen", "search-image", "/tmp/template.png", "--similarity", "-Infinity"]);
        var legacyDownsample = CliCommandRouterAccessor.Parse(["screen", "search-image", "/tmp/template.png", "--downsample", "2"]);
        var legacyScaleAware = CliCommandRouterAccessor.Parse(["screen", "search-image", "/tmp/template.png", "--scale-aware"]);
        var badRegion = CliCommandRouterAccessor.Parse(["screen", "search-image", "/tmp/template.png", "--region", "1", "2", "0", "4"]);
        var badWaitTimeout = CliCommandRouterAccessor.Parse(["screen", "wait-image", "/tmp/template.png", "--timeout-ms", "-1"]);
        var badImageClickButton = CliCommandRouterAccessor.Parse(["screen", "image-click", "/tmp/template.png", "--button", "side"]);
        var badMatchMode = CliCommandRouterAccessor.Parse(["screen", "search-image", "/tmp/template.png", "--matchmode", "middle"]);
        var duplicateMatchMode = CliCommandRouterAccessor.Parse(["screen", "search-image", "/tmp/template.png", "--matchmode", "first", "--matchmode", "best"]);
        var instantTimeout = CliCommandRouterAccessor.Parse(["screen", "search-image", "/tmp/template.png", "--timeout-ms", "1"]);
        var instantPixelTimeout = CliCommandRouterAccessor.Parse(["screen", "pixel", "1", "2", "--timeout-ms", "1"]);
        var retiredPoll = CliCommandRouterAccessor.Parse(["screen", "search-image", "/tmp/template.png", "--poll"]);

        Assert.False(badSimilarity.IsSuccess);
        Assert.Contains("--similarity", badSimilarity.ErrorMessage, StringComparison.Ordinal);
        Assert.False(badSimilarityNaN.IsSuccess);
        Assert.Contains("--similarity", badSimilarityNaN.ErrorMessage, StringComparison.Ordinal);
        Assert.False(badSimilarityInfinity.IsSuccess);
        Assert.Contains("--similarity", badSimilarityInfinity.ErrorMessage, StringComparison.Ordinal);
        Assert.False(badSimilarityNegativeInfinity.IsSuccess);
        Assert.Contains("--similarity", badSimilarityNegativeInfinity.ErrorMessage, StringComparison.Ordinal);
        Assert.False(legacyDownsample.IsSuccess);
        Assert.Contains("Unknown option for screen search-image: --downsample", legacyDownsample.ErrorMessage, StringComparison.Ordinal);
        Assert.False(legacyScaleAware.IsSuccess);
        Assert.Contains("Unknown option for screen search-image: --scale-aware", legacyScaleAware.ErrorMessage, StringComparison.Ordinal);
        Assert.False(badRegion.IsSuccess);
        Assert.Contains("--region", badRegion.ErrorMessage, StringComparison.Ordinal);
        Assert.False(badWaitTimeout.IsSuccess);
        Assert.Contains("--timeout-ms", badWaitTimeout.ErrorMessage, StringComparison.Ordinal);
        Assert.False(badImageClickButton.IsSuccess);
        Assert.Contains("--button", badImageClickButton.ErrorMessage, StringComparison.Ordinal);
        Assert.False(badMatchMode.IsSuccess);
        Assert.Contains("--matchmode", badMatchMode.ErrorMessage, StringComparison.Ordinal);
        Assert.False(duplicateMatchMode.IsSuccess);
        Assert.Contains("Duplicate --matchmode", duplicateMatchMode.ErrorMessage, StringComparison.Ordinal);
        Assert.False(instantTimeout.IsSuccess);
        Assert.Contains("Unknown option for screen search-image: --timeout-ms", instantTimeout.ErrorMessage, StringComparison.Ordinal);
        Assert.False(instantPixelTimeout.IsSuccess);
        Assert.Contains("Unknown option for screen pixel: --timeout-ms", instantPixelTimeout.ErrorMessage, StringComparison.Ordinal);
        Assert.False(retiredPoll.IsSuccess);
        Assert.Contains("Unknown option for screen search-image: --poll", retiredPoll.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_WhenScreenshotOutputAndRegion_ReturnsOptions()
    {
        var result = CliCommandRouterAccessor.Parse(["screenshot", "--output", "/tmp/shot.png", "--region", "1", "2", "30", "40", "--json"]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<ScreenshotCliOptions>(result.Options);
        Assert.Equal(ScreenshotCliAction.Capture, options.Action);
        Assert.Equal("/tmp/shot.png", options.OutputPath);
        Assert.Equal(1, options.RegionX);
        Assert.Equal(2, options.RegionY);
        Assert.Equal(30, options.RegionWidth);
        Assert.Equal(40, options.RegionHeight);
        Assert.True(options.JsonOutput);
    }

    [Fact]
    public void Parse_WhenScreenshotClipboard_ReturnsOptions()
    {
        var result = CliCommandRouterAccessor.Parse(["screenshot", "--clipboard", "--json"]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<ScreenshotCliOptions>(result.Options);
        Assert.True(options.Clipboard);
        Assert.Null(options.OutputPath);
        Assert.True(options.JsonOutput);
    }

    [Fact]
    public void Parse_WhenScreenshotOutputAndClipboard_ReturnsOptions()
    {
        var result = CliCommandRouterAccessor.Parse(["screenshot", "-o", "/tmp/shot.png", "--clipboard"]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<ScreenshotCliOptions>(result.Options);
        Assert.Equal("/tmp/shot.png", options.OutputPath);
        Assert.True(options.Clipboard);
    }

    [Fact]
    public void Parse_WhenScreenshotMissingOutput_ReturnsError()
    {
        var result = CliCommandRouterAccessor.Parse(["screenshot", "--region", "1", "2", "3", "4"]);

        Assert.False(result.IsSuccess);
        Assert.Contains("--output", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("--clipboard", result.ErrorMessage, StringComparison.Ordinal);
    }
}
