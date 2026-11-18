using CrossMacro.Core.Models.Macros;
using CrossMacro.Platform.Abstractions.ScreenReading;

namespace CrossMacro.Cli.Tests;

public sealed class ScreenWindowParsingTests
{
    [Theory]
    [InlineData("AUTO", true, ScreenImageMatchMode.Automatic)]
    [InlineData("First", true, ScreenImageMatchMode.First)]
    [InlineData("bEsT", true, ScreenImageMatchMode.Best)]
    [InlineData(" first ", false, ScreenImageMatchMode.First)]
    [InlineData("", false, ScreenImageMatchMode.First)]
    [InlineData("nearest", false, ScreenImageMatchMode.First)]
    public void ImageMatchMode_PreservesCliTokenBoundaries(string token, bool accepted, ScreenImageMatchMode expected)
    {
        var result = CliCommandRouter.Parse(["screen", "search-image", "template.png", "--matchmode", token]);

        Assert.Equal(accepted, result.IsSuccess);
        if (accepted)
        {
            Assert.Equal(expected, Assert.IsType<ScreenOptions>(result.Options).MatchMode);
        }
        else
        {
            Assert.Contains("--matchmode must be auto, first, or best", result.ErrorMessage, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Parse_WhenRunWithInlineScreenReadingSteps_ReturnsRunOptions()
    {
        var result = CliCommandRouter.Parse([
            "run",
            "pixelcolor", "1", "2", "sampled",
            "waitcolor", "3", "4", "00FF00", "100", "wait_ok",
            "pixelsearch", "0", "0", "10", "10", "FF0000", "found", "found_x", "found_y", "tolerance", "26",
            "click", "left",
        ]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<RunOptions>(result.Options);
        Assert.Equal("pixelcolor 1 2 sampled", options.Steps[0]);
        Assert.Equal("waitcolor 3 4 00FF00 100 wait_ok", options.Steps[1]);
        Assert.Equal("pixelsearch 0 0 10 10 FF0000 found found_x found_y tolerance 26", options.Steps[2]);
        Assert.Equal("click left", options.Steps[3]);
    }

    [Fact]
    public void Parse_WhenRunWithInlineScreenReadingOptionalForms_ReturnsRunOptions()
    {
        var result = CliCommandRouter.Parse([
            "run",
            "pixelcolor", "rel", "-1", "2",
            "pixelcolor", "rel", "-3", "4", "relativeSampled",
            "waitcolor", "3", "4", "00FF00",
            "pixelsearch", "0", "0", "10", "10", "FF0000", "tolerance", "26",
            "pixelsearch", "1", "2", "11", "12", "00FF00", "found_x", "found_y", "tolerance", "7",
            "click", "left",
        ]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<RunOptions>(result.Options);
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
        var result = CliCommandRouter.Parse(["run", "pixelcolorful", "1", "2", "sampled"]);

        Assert.False(result.IsSuccess);
        Assert.Contains("Unknown inline run step command: pixelcolorful", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_WhenWindowCommands_ReturnTypedOptions()
    {
        var search = CliCommandRouter.Parse(["window", "search", "--title", "Firefox", "--json"]);
        var wait = CliCommandRouter.Parse(["window", "wait", "--class", "Code", "--timeout-ms", "1500"]);
        var move = CliCommandRouter.Parse(["window", "move", "--active", "10", "20"]);
        var workspace = CliCommandRouter.Parse(["window", "workspace", "move-window", "--address", "0xabc", "dev"]);

        Assert.True(search.IsSuccess);
        Assert.Equal(WindowAction.Search, Assert.IsType<WindowOptions>(search.Options).Action);
        Assert.True(Assert.IsType<WindowOptions>(search.Options).JsonOutput);
        Assert.True(wait.IsSuccess);
        Assert.Equal(1500, Assert.IsType<WindowOptions>(wait.Options).TimeoutMs);
        Assert.True(move.IsSuccess);
        Assert.Equal(10, Assert.IsType<WindowOptions>(move.Options).X);
        Assert.True(workspace.IsSuccess);
        var workspaceOptions = Assert.IsType<WindowOptions>(workspace.Options);
        Assert.Equal(WindowAction.WorkspaceMoveWindow, workspaceOptions.Action);
        Assert.Equal("dev", workspaceOptions.WorkspaceName);
    }

    [Fact]
    public void Parse_WhenWindowActionVariantsAreUsed_PreservesActionAndOperands()
    {
        var active = CliCommandRouter.Parse(["window", "active"]);
        var list = CliCommandRouter.Parse(["window", "list"]);
        var search = CliCommandRouter.Parse(["window", "search", "--title", "Firefox"]);
        var wait = CliCommandRouter.Parse(["window", "wait", "--class", "Code", "--timeout-ms", "250"]);
        var focus = CliCommandRouter.Parse(["window", "focus", "--address", "0xabc"]);
        var close = CliCommandRouter.Parse(["window", "close", "--title", "Firefox"]);
        var move = CliCommandRouter.Parse(["window", "move", "--active", "-10", "20"]);
        var resize = CliCommandRouter.Parse(["window", "resize", "--active", "800", "600"]);
        var center = CliCommandRouter.Parse(["window", "center", "--active"]);
        var maximize = CliCommandRouter.Parse(["window", "maximize", "--active"]);
        var fullscreen = CliCommandRouter.Parse(["window", "fullscreen", "--active"]);
        var floating = CliCommandRouter.Parse(["window", "float", "--active"]);
        var workspaceGet = CliCommandRouter.Parse(["window", "workspace", "get"]);
        var workspaceSwitch = CliCommandRouter.Parse(["window", "workspace", "switch", "dev"]);
        var workspaceMoveActive = CliCommandRouter.Parse(["window", "workspace", "move-active", "dev"]);
        var workspaceMoveWindow = CliCommandRouter.Parse(["window", "workspace", "move-window", "--address", "0xabc", "dev"]);

        Assert.Equal(WindowAction.Active, Assert.IsType<WindowOptions>(active.Options).Action);
        Assert.Equal(WindowAction.List, Assert.IsType<WindowOptions>(list.Options).Action);
        Assert.Equal(WindowAction.Search, Assert.IsType<WindowOptions>(search.Options).Action);
        var waitOptions = Assert.IsType<WindowOptions>(wait.Options);
        Assert.Equal(WindowAction.Wait, waitOptions.Action);
        Assert.Equal(WindowSelectorKind.Class, waitOptions.Selector!.Kind);
        Assert.Equal(250, waitOptions.TimeoutMs);
        Assert.Equal(WindowAction.Focus, Assert.IsType<WindowOptions>(focus.Options).Action);
        Assert.Equal(WindowAction.Close, Assert.IsType<WindowOptions>(close.Options).Action);
        var moveOptions = Assert.IsType<WindowOptions>(move.Options);
        Assert.Equal(WindowAction.Move, moveOptions.Action);
        Assert.Equal(-10, moveOptions.X);
        Assert.Equal(20, moveOptions.Y);
        var resizeOptions = Assert.IsType<WindowOptions>(resize.Options);
        Assert.Equal(WindowAction.Resize, resizeOptions.Action);
        Assert.Equal(800, resizeOptions.Width);
        Assert.Equal(600, resizeOptions.Height);
        Assert.Equal(WindowAction.Center, Assert.IsType<WindowOptions>(center.Options).Action);
        Assert.Equal(WindowAction.Maximize, Assert.IsType<WindowOptions>(maximize.Options).Action);
        Assert.Equal(WindowAction.Fullscreen, Assert.IsType<WindowOptions>(fullscreen.Options).Action);
        Assert.Equal(WindowAction.Floating, Assert.IsType<WindowOptions>(floating.Options).Action);
        Assert.Equal(WindowAction.WorkspaceGet, Assert.IsType<WindowOptions>(workspaceGet.Options).Action);
        Assert.Equal(WindowAction.WorkspaceSwitch, Assert.IsType<WindowOptions>(workspaceSwitch.Options).Action);
        Assert.Equal(WindowAction.WorkspaceMoveActive, Assert.IsType<WindowOptions>(workspaceMoveActive.Options).Action);
        var moveWindowOptions = Assert.IsType<WindowOptions>(workspaceMoveWindow.Options);
        Assert.Equal(WindowAction.WorkspaceMoveWindow, moveWindowOptions.Action);
        Assert.Equal(WindowSelectorKind.Address, moveWindowOptions.Selector!.Kind);
        Assert.Equal("0xabc", moveWindowOptions.Selector.Value);
        Assert.Equal("dev", moveWindowOptions.WorkspaceName);
    }

    [Fact]
    public void Parse_WhenWindowFocusHasMultipleSelectors_ReturnsError()
    {
        var result = CliCommandRouter.Parse(["window", "focus", "--title", "A", "--class", "B"]);

        Assert.False(result.IsSuccess);
        Assert.Contains("Only one window selector", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_WhenScreenCommands_ReturnTypedOptions()
    {
        var pixel = CliCommandRouter.Parse(["screen", "pixel", "--relative", "-1", "2", "--json"]);
        var wait = CliCommandRouter.Parse(["screen", "wait-color", "3", "4", "00ff00", "--timeout-ms", "500"]);
        var search = CliCommandRouter.Parse(["screen", "search-color", "0", "0", "10", "20", "FF0000", "--timeout-ms", "450", "--tolerance", "26"]);
        var imageSearch = CliCommandRouter.Parse(["screen", "search-image", "/tmp/template.png", "--region", "1", "2", "30", "40", "--similarity", "0.9", "--json"]);
        var firstImageSearch = CliCommandRouter.Parse(["screen", "search-image", "/tmp/template.png", "--matchmode", "first"]);
        var bestImageSearch = CliCommandRouter.Parse(["screen", "search-image", "/tmp/template.png", "--matchmode", "best"]);
        var automaticImageSearch = CliCommandRouter.Parse(["screen", "search-image", "/tmp/template.png"]);
        var waitImage = CliCommandRouter.Parse(["screen", "wait-image", "/tmp/template.png", "--timeout-ms", "750", "--region", "2", "3", "40", "50", "--similarity", "0.8"]);
        var imageClick = CliCommandRouter.Parse(["screen", "image-click", "/tmp/template.png", "--timeout-ms", "650", "--button", "right", "--region", "4", "5", "60", "70", "--similarity", "0.7"]);

        Assert.True(pixel.IsSuccess);
        var pixelOptions = Assert.IsType<ScreenOptions>(pixel.Options);
        Assert.True(pixelOptions.Relative);
        Assert.Equal(-1, pixelOptions.X);
        Assert.Null(pixelOptions.TimeoutMs);
        Assert.True(pixelOptions.JsonOutput);
        Assert.True(wait.IsSuccess);
        var waitOptions = Assert.IsType<ScreenOptions>(wait.Options);
        Assert.Equal(500, waitOptions.TimeoutMs);
        Assert.True(search.IsSuccess);
        var searchOptions = Assert.IsType<ScreenOptions>(search.Options);
        Assert.Equal(26, searchOptions.Tolerance);
        Assert.Equal(450, searchOptions.TimeoutMs);
        Assert.True(imageSearch.IsSuccess);
        var imageSearchOptions = Assert.IsType<ScreenOptions>(imageSearch.Options);
        Assert.Equal(ScreenAction.SearchImage, imageSearchOptions.Action);
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
        Assert.Equal(ScreenImageMatchMode.First, Assert.IsType<ScreenOptions>(firstImageSearch.Options).MatchMode);
        Assert.True(bestImageSearch.IsSuccess);
        Assert.Equal(ScreenImageMatchMode.Best, Assert.IsType<ScreenOptions>(bestImageSearch.Options).MatchMode);
        Assert.True(automaticImageSearch.IsSuccess);
        var automaticImageSearchOptions = Assert.IsType<ScreenOptions>(automaticImageSearch.Options);
        Assert.Equal(ScreenImageMatchMode.Automatic, automaticImageSearchOptions.MatchMode);
        Assert.Equal(0.95, automaticImageSearchOptions.Similarity);
        Assert.True(waitImage.IsSuccess);
        var waitImageOptions = Assert.IsType<ScreenOptions>(waitImage.Options);
        Assert.Equal(ScreenAction.WaitImage, waitImageOptions.Action);
        Assert.Equal(750, waitImageOptions.TimeoutMs);
        Assert.Equal(2, waitImageOptions.RegionX);
        Assert.Equal(3, waitImageOptions.RegionY);
        Assert.Equal(40, waitImageOptions.RegionWidth);
        Assert.Equal(50, waitImageOptions.RegionHeight);
        Assert.Equal(0.8, waitImageOptions.Similarity);
        Assert.True(imageClick.IsSuccess);
        var imageClickOptions = Assert.IsType<ScreenOptions>(imageClick.Options);
        Assert.Equal(ScreenAction.ImageClick, imageClickOptions.Action);
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
        var move = CliCommandRouter.Parse(["move", "abs", "10", "20", "--dry-run"]);
        var click = CliCommandRouter.Parse(["click", "current", "right", "--json"]);
        var type = CliCommandRouter.Parse(["type", "hello world", "--dry-run"]);
        var preciseDelay = CliCommandRouter.Parse(["delay", "2.375ms"]);
        var delay = CliCommandRouter.Parse(["delay", "random", "10", "20"]);

        Assert.True(move.IsSuccess);
        var moveOptions = Assert.IsType<InputOptions>(move.Options);
        Assert.Equal("move abs 10 20", moveOptions.Step);
        Assert.True(moveOptions.DryRun);
        Assert.True(click.IsSuccess);
        Assert.Equal("click current right", Assert.IsType<InputOptions>(click.Options).Step);
        Assert.True(Assert.IsType<InputOptions>(click.Options).JsonOutput);
        Assert.True(type.IsSuccess);
        Assert.Equal("type hello world", Assert.IsType<InputOptions>(type.Options).Step);
        Assert.True(preciseDelay.IsSuccess);
        Assert.Equal("delay 2.375ms", Assert.IsType<InputOptions>(preciseDelay.Options).Step);
        Assert.True(delay.IsSuccess);
        Assert.Equal("delay random 10 20", Assert.IsType<InputOptions>(delay.Options).Step);
    }

    [Fact]
    public void Parse_WhenTopLevelInputUsesRelativeAlias_PreservesStepAndCommonOptions()
    {
        var result = CliCommandRouter.Parse(["move", "relative-logical", "-3", "4", "--dry-run", "--log-level", "debug"]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<InputOptions>(result.Options);
        Assert.Equal("move relative-logical -3 4", options.Step);
        Assert.True(options.DryRun);
        Assert.False(options.JsonOutput);
        Assert.Equal("Debug", options.LogLevel);
    }

    [Fact]
    public void Parse_WhenRunHasImageAssetsAndPixelSearchTimeout_PreservesBoth()
    {
        var result = CliCommandRouter.Parse([
            "run",
            "--asset", "button", "/tmp/button.png",
            "pixelsearch", "0", "0", "10", "10", "FF0000", "found", "x", "y", "timeout", "5000", "tolerance", "5",
            "--dry-run",
        ]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<RunOptions>(result.Options);
        Assert.True(options.DryRun);
        Assert.Equal("/tmp/button.png", Assert.Single(options.ImageAssets!).FilePath);
        Assert.Equal("button", options.ImageAssets![0].Name);
        Assert.Equal("pixelsearch 0 0 10 10 FF0000 found x y timeout 5000 tolerance 5", Assert.Single(options.Steps));
    }

    [Theory]
    [InlineData("_icon", true)]
    [InlineData("Ícone", true)]
    [InlineData("target_1", true)]
    [InlineData("1target", false)]
    [InlineData("$target", false)]
    [InlineData("tar-get", false)]
    public void Parse_WhenRunAssetNameIsGiven_AppliesTheSharedAssetNameRule(string name, bool accepted)
    {
        var result = CliCommandRouter.Parse(["run", "--asset", name, "/tmp/button.png", "click", "left"]);

        Assert.Equal(accepted, result.IsSuccess);
        if (accepted)
        {
            Assert.Equal(name, Assert.Single(Assert.IsType<RunOptions>(result.Options).ImageAssets!).Name);
        }
    }

    [Fact]
    public void Parse_WhenRunInlineRuntimeStepHasNegativeOperands_PreservesTheStep()
    {
        var result = CliCommandRouter.Parse(["run", "window", "move", "-10", "-20", "click", "left"]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<RunOptions>(result.Options);
        Assert.Equal(["window move -10 -20", "click left"], options.Steps);
    }

    [Fact]
    public void Parse_WhenRunImageAssetNameIsMalformed_ReturnsError()
    {
        var result = CliCommandRouter.Parse(["run", "--asset", "bad-name", "/tmp/button.png", "--step", "click left"]);

        Assert.False(result.IsSuccess);
        Assert.Contains("Invalid run image asset name", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_WhenScreenPollingOptionsAreUsed_ReturnsErrors()
    {
        var color = CliCommandRouter.Parse(["screen", "search-color", "0", "0", "10", "10", "FF0000", "--timeout-ms", "5000", "--poll-ms", "25"]);
        var image = CliCommandRouter.Parse(["screen", "search-image", "/tmp/button.png", "--poll"]);

        Assert.False(color.IsSuccess);
        Assert.Contains("Unknown option for screen search-color: --poll-ms", color.ErrorMessage, StringComparison.Ordinal);
        Assert.False(image.IsSuccess);
        Assert.Contains("Unknown option for screen search-image: --poll", image.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_WhenScreenSearchToleranceOutOfRange_ReturnsError()
    {
        var result = CliCommandRouter.Parse(["screen", "search-color", "0", "0", "10", "20", "FF0000", "--tolerance", "256"]);

        Assert.False(result.IsSuccess);
        Assert.Contains("--tolerance", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_WhenScreenSearchImageMissingPath_ReturnsError()
    {
        var result = CliCommandRouter.Parse(["screen", "search-image", "--json"]);

        Assert.False(result.IsSuccess);
        Assert.Equal("screen search-image requires <image-path>.", result.ErrorMessage);
        Assert.True(result.PrefersJsonOutput);
    }

    [Fact]
    public void Parse_WhenScreenSearchImageInvalidOptions_ReturnsError()
    {
        var badSimilarity = CliCommandRouter.Parse(["screen", "search-image", "/tmp/template.png", "--similarity", "1.1"]);
        var badSimilarityNaN = CliCommandRouter.Parse(["screen", "search-image", "/tmp/template.png", "--similarity", "NaN"]);
        var badSimilarityInfinity = CliCommandRouter.Parse(["screen", "search-image", "/tmp/template.png", "--similarity", "Infinity"]);
        var badSimilarityNegativeInfinity = CliCommandRouter.Parse(["screen", "search-image", "/tmp/template.png", "--similarity", "-Infinity"]);
        var legacyDownsample = CliCommandRouter.Parse(["screen", "search-image", "/tmp/template.png", "--downsample", "2"]);
        var legacyScaleAware = CliCommandRouter.Parse(["screen", "search-image", "/tmp/template.png", "--scale-aware"]);
        var badRegion = CliCommandRouter.Parse(["screen", "search-image", "/tmp/template.png", "--region", "1", "2", "0", "4"]);
        var badWaitTimeout = CliCommandRouter.Parse(["screen", "wait-image", "/tmp/template.png", "--timeout-ms", "-1"]);
        var badImageClickButton = CliCommandRouter.Parse(["screen", "image-click", "/tmp/template.png", "--button", "side"]);
        var badMatchMode = CliCommandRouter.Parse(["screen", "search-image", "/tmp/template.png", "--matchmode", "middle"]);
        var duplicateMatchMode = CliCommandRouter.Parse(["screen", "search-image", "/tmp/template.png", "--matchmode", "first", "--matchmode", "best"]);
        var instantTimeout = CliCommandRouter.Parse(["screen", "search-image", "/tmp/template.png", "--timeout-ms", "1"]);
        var instantPixelTimeout = CliCommandRouter.Parse(["screen", "pixel", "1", "2", "--timeout-ms", "1"]);
        var retiredPoll = CliCommandRouter.Parse(["screen", "search-image", "/tmp/template.png", "--poll"]);

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
        var result = CliCommandRouter.Parse(["screenshot", "--output", "/tmp/shot.png", "--region", "1", "2", "30", "40", "--json"]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<ScreenshotOptions>(result.Options);
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
        var result = CliCommandRouter.Parse(["screenshot", "--clipboard", "--json"]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<ScreenshotOptions>(result.Options);
        Assert.True(options.Clipboard);
        Assert.Null(options.OutputPath);
        Assert.True(options.JsonOutput);
    }

    [Fact]
    public void Parse_WhenScreenshotOutputAndClipboard_ReturnsOptions()
    {
        var result = CliCommandRouter.Parse(["screenshot", "-o", "/tmp/shot.png", "--clipboard"]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<ScreenshotOptions>(result.Options);
        Assert.Equal("/tmp/shot.png", options.OutputPath);
        Assert.True(options.Clipboard);
    }

    [Fact]
    public void Parse_WhenScreenshotMissingOutput_ReturnsError()
    {
        var result = CliCommandRouter.Parse(["screenshot", "--region", "1", "2", "3", "4"]);

        Assert.False(result.IsSuccess);
        Assert.Contains("--output", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("--clipboard", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--json")]
    [InlineData("--clipboard")]
    public void Parse_WhenScreenshotOutputValueIsAnotherOption_ReturnsError(string option)
    {
        var result = CliCommandRouter.Parse(["screenshot", "--output", option]);

        Assert.False(result.IsSuccess);
        Assert.Equal("--output requires a file path.", result.ErrorMessage);
    }
}
