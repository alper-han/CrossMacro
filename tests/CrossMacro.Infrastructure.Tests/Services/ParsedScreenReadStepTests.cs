namespace CrossMacro.Infrastructure.Tests.Services;

public sealed class ParsedScreenReadStepTests
{
    [Theory]
    [InlineData("imagesearch", "ImageSearch")]
    [InlineData("waitimage", "WaitImage")]
    [InlineData("imageclick", "ImageClick")]
    public void Parse_ExplicitImageRegion_PreservesRuntimeOperandsAndTypedOptions(string command, string expected)
    {
        var recognized = RunScriptScreenReadingStepParser.TryParseStep(
            $"{command} region $left -2 $width 40 icon found x y matchmode BeSt similarity 0.8", out var parsed, out var error);

        Assert.True(recognized);
        Assert.Null(error);
        var image = Assert.IsType<ParsedScreenReadStep.Image>(parsed);
        Assert.Equal(expected, image.Command.ToString());
        Assert.Equal(new ParsedScreenReadStep.ExplicitRegion("$left", "-2", "$width", "40"), image.Region);
        Assert.Equal("icon", image.ImageName);
        Assert.Equal(new PixelSearchVariableLayout("found", "x", "y"), image.Variables);
        Assert.Equal(0.8, image.Similarity);
        Assert.Equal(EditorImageMatchMode.BestMatch, image.MatchMode);
        Assert.True(image.MatchModeExplicit);
    }

    [Fact]
    public void Parse_LegacyImageRegion_PreservesEndpointMeaning()
    {
        Assert.True(RunScriptScreenReadingStepParser.TryParseStep("imageclick -5 -2 10 20 icon timeout 7 button right", out var parsed, out var error));
        Assert.Null(error);
        var image = Assert.IsType<ParsedScreenReadStep.Image>(parsed);
        Assert.Equal(new ParsedScreenReadStep.LegacyRegion(-5, -2, 10, 20), image.Region);
        Assert.Equal(7, image.TimeoutMs);
        Assert.Equal(MacroMouseButton.Right, image.Button);
    }

    [Theory]
    [InlineData("imagesearch icon timeout 10", "Unknown imagesearch option 'timeout'. Expected similarity <0..1> or matchmode <auto|first|best>.")]
    [InlineData("imagesearch icon similarity 0.5 similarity 0.6", "Duplicate imagesearch similarity option.")]
    public void Parse_InvalidOptions_RetainsValidationMessage(string step, string expectedError)
    {
        Assert.True(RunScriptScreenReadingStepParser.TryParseStep(step, out var parsed, out var error));
        Assert.Null(parsed);
        Assert.Equal(expectedError, error);
    }

    [Theory]
    [InlineData("pixelsearch 9 8 1 2 $color x y timeout 15 tolerance 2", null, "x", "y")]
    [InlineData("pixelsearch 9 8 1 2 $color found x y timeout 15 tolerance 2", "found", "x", "y")]
    public void Parse_PixelSearch_PreservesDirectionAndOutputLayout(string text, string? found, string x, string y)
    {
        Assert.True(RunScriptScreenReadingStepParser.TryParseStep(text, out var parsed, out var error));
        Assert.Null(error);
        var search = Assert.IsType<ParsedScreenReadStep.PixelSearch>(parsed);
        Assert.Equal((9, 8, 1, 2), (search.X1, search.Y1, search.X2, search.Y2));
        Assert.Equal(new PixelSearchVariableLayout(found, x, y), search.Variables);
        Assert.Equal(15, search.TimeoutMs);
        Assert.Equal(2, search.Tolerance);
        Assert.Equal("$color", search.ColorToken);
    }

    [Theory]
    [InlineData("pixelcolor rel -2 4 color", EditorActionType.PixelColor)]
    [InlineData("waitcolor 1 2 $wanted 5 found", EditorActionType.WaitColor)]
    [InlineData("pixelsearch 1 2 8 10 FF0011 found x y tolerance 2 timeout 8", EditorActionType.PixelSearch)]
    [InlineData("imagesearch region $left 0 10 20 icon found x y matchmode first", EditorActionType.ImageSearch)]
    [InlineData("imageclick 0 0 10 20 icon timeout 12 button middle", EditorActionType.ImageClick)]
    [InlineData("waitimage icon timeout 9 similarity 0.8", EditorActionType.WaitImage)]
    public void Parse_EditorProjectionAndCompilerConsumeSameValidCommand(string text, EditorActionType expected)
    {
        var mapper = Substitute.For<IKeyCodeMapper>();
        Assert.True(EditorScriptReader.TryParseScreenReadingStep(text, out var action));
        Assert.Equal(expected, action.Type);
        var result = new RunScriptCompiler(mapper).Compile([new RunScriptStep(text)]);
        Assert.True(result.Success, result.ErrorMessage);
    }

    [Theory]
    [InlineData("pixelcolor 1 2 $color", "color")]
    [InlineData("waitcolor 1 2 FF0011 5 $color", "color")]
    [InlineData("imagesearch icon $found $x $y", "found")]
    public void Parse_EditorNormalizesOutputNamesWhileRuntimeSyntaxPreservesThem(string text, string normalized)
    {
        Assert.True(RunScriptScreenReadingStepParser.TryParseStep(text, out var parsed, out var error));
        Assert.Null(error);
        Assert.True(EditorScriptReader.TryParseScreenReadingStep(text, out var action));

        if (parsed is ParsedScreenReadStep.Image image)
        {
            Assert.Equal("$found", image.Variables.FoundVariableName);
            Assert.Equal(normalized, action.ScreenFoundVariableName);
            Assert.Equal("x", action.ScreenFoundXVariableName);
            Assert.Equal("y", action.ScreenFoundYVariableName);
        }
        else
        {
            Assert.Equal(normalized, action.ScreenColorVariableName);
            var variable = parsed is ParsedScreenReadStep.PixelColor pixel
                ? pixel.ResultVariable : Assert.IsType<ParsedScreenReadStep.WaitColor>(parsed).ResultVariable;
            Assert.Equal("$color", variable);
        }
    }
}
