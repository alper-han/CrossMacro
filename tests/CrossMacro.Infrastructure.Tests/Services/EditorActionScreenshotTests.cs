
namespace CrossMacro.Infrastructure.Tests.Services;

public sealed class EditorActionScreenshotTests
{
    private readonly EditorActionConverter _converter;
    private readonly EditorActionValidator _validator;

    public EditorActionScreenshotTests()
    {
        var keyCodeMapper = Substitute.For<IKeyCodeMapper>();
        keyCodeMapper.GetKeyCode(Arg.Any<string>()).Returns(-1);
        keyCodeMapper.GetKeyCodeForCharacter(Arg.Any<char>()).Returns(-1);
        _converter = new EditorActionConverter(keyCodeMapper);
        _validator = new EditorActionValidator(_converter);
    }

    [Fact]
    public void ToMacroSequence_WhenScreenshotHasQuotedOutputAndRegion_SerializesScriptStep()
    {
        var action = new EditorAction
        {
            Type = EditorActionType.Screenshot,
            ScreenshotOutputPath = "path with spaces.png",
            ScreenshotCopyToClipboard = true,
            ScreenshotUseRegion = true,
            ScreenshotRegionX = "0",
            ScreenshotRegionY = "0",
            ScreenshotRegionWidth = "100",
            ScreenshotRegionHeight = "100",
        };

        var sequence = _converter.ToMacroSequence([action], "Screenshot", isAbsolute: false);

        sequence.ScriptSteps.Should().Equal("screenshot region 0 0 100 100 output \"path with spaces.png\" clipboard");
    }

    [Fact]
    public void ToMacroSequence_WhenScreenshotOutputContainsQuoteOrBackslash_EscapesQuotedPath()
    {
        var action = new EditorAction
        {
            Type = EditorActionType.Screenshot,
            ScreenshotOutputPath = "C:\\shots\\say \"hi\".png",
        };

        var sequence = _converter.ToMacroSequence([action], "Screenshot", isAbsolute: false);

        sequence.ScriptSteps.Should().Equal("screenshot output \"C:\\\\shots\\\\say \\\"hi\\\".png\"");
    }

    [Fact]
    public void FromMacroSequence_WhenScreenshotHasQuotedOutputAndRegion_RestoresStructuredAction()
    {
        var sequence = new MacroSequence
        {
            ScriptSteps = ["screenshot region 0 0 100 100 output \"path with spaces.png\" clipboard"],
        };

        var action = _converter.FromMacroSequence(sequence).Should().ContainSingle().Subject;

        action.Type.Should().Be(EditorActionType.Screenshot);
        action.ScreenshotOutputPath.Should().Be("path with spaces.png");
        action.ScreenshotCopyToClipboard.Should().BeTrue();
        action.ScreenshotUseRegion.Should().BeTrue();
        action.ScreenshotRegionWidth.Should().Be("100");
        action.ScreenshotRegionHeight.Should().Be("100");
    }

    [Fact]
    public void FromMacroSequence_WhenScreenshotOutputContainsEscapedQuoteOrBackslash_RestoresPath()
    {
        var sequence = new MacroSequence
        {
            ScriptSteps = ["screenshot output \"C:\\\\shots\\\\say \\\"hi\\\".png\""],
        };

        var action = _converter.FromMacroSequence(sequence).Should().ContainSingle().Subject;

        action.Type.Should().Be(EditorActionType.Screenshot);
        action.ScreenshotOutputPath.Should().Be("C:\\shots\\say \"hi\".png");
    }

    [Fact]
    public void FromMacroSequence_WhenScreenshotHasSimpleOutput_RestoresStructuredAction()
    {
        var sequence = new MacroSequence
        {
            ScriptSteps = ["screenshot output simple.png"],
        };

        var action = _converter.FromMacroSequence(sequence).Should().ContainSingle().Subject;

        action.Type.Should().Be(EditorActionType.Screenshot);
        action.ScreenshotOutputPath.Should().Be("simple.png");
        action.ScreenshotCopyToClipboard.Should().BeFalse();
    }

    [Fact]
    public void FromMacroSequence_WhenScreenshotSyntaxIsMalformed_PreservesRawScriptStep()
    {
        var sequence = new MacroSequence
        {
            ScriptSteps = ["screenshot output \"unterminated path"],
        };

        var action = _converter.FromMacroSequence(sequence).Should().ContainSingle().Subject;

        action.Type.Should().Be(EditorActionType.RawScriptStep);
        action.Text.Should().Be("screenshot output \"unterminated path");
    }

    [Fact]
    public void Validate_WhenScreenshotHasNoDestination_ReturnsInvalid()
    {
        var action = new EditorAction { Type = EditorActionType.Screenshot };

        var result = _validator.Validate(action);

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("output path or clipboard");
    }

    [Theory]
    [InlineData("0", "0", "0", "10", "width/height")]
    public void Validate_WhenScreenshotRegionIsInvalid_ReturnsInvalid(string x, string y, string width, string height, string errorText)
    {
        var action = new EditorAction
        {
            Type = EditorActionType.Screenshot,
            ScreenshotCopyToClipboard = true,
            ScreenshotUseRegion = true,
            ScreenshotRegionX = x,
            ScreenshotRegionY = y,
            ScreenshotRegionWidth = width,
            ScreenshotRegionHeight = height,
        };

        var result = _validator.Validate(action);

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain(errorText);
    }

    [Fact]
    public void ValidateAll_WhenScreenshotHasQuotedOutputPath_CompilesEmittedScript()
    {
        var action = new EditorAction
        {
            Type = EditorActionType.Screenshot,
            ScreenshotOutputPath = "path with spaces.png",
        };

        var result = _validator.ValidateAll([action]);

        result.IsValid.Should().BeTrue(string.Join("; ", result.Errors));
    }

    [Theory]
    [InlineData("screenshot output \"path with spaces.png\"")]
    [InlineData("screenshot output \"C:\\\\shots\\\\say \\\"hi\\\".png\"")]
    [InlineData("screenshot region 0 0 100 100 output \"path with spaces.png\" clipboard")]
    public void ValidateScreenshotStep_WhenOutputPathIsQuoted_ReturnsValid(string step)
    {
        RunScriptPlatformSyntax.ValidateScreenshotStep(step).Should().BeNull();
    }
}
