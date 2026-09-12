
namespace CrossMacro.Infrastructure.Tests.Services;

public sealed class RunScriptRuntimeStepClassifierTests
{
    [Theory]
    [InlineData("screenshot clipboard")]
    [InlineData("screenshot region 1 2 3 4 output shot.png clipboard")]
    public void IsRuntimeStep_WhenScreenshotStep_ReturnsTrue(string step)
    {
        _ = RunScriptRuntimeStepClassifier.IsRuntimeStep(step).Should().BeTrue();
    }

    [Fact]
    public void IsRuntimeStep_WhenMousePositionStep_ReturnsTrue()
    {
        _ = RunScriptRuntimeStepClassifier.IsRuntimeStep("mouse position x y").Should().BeTrue();
    }

    [Theory]
    [InlineData("pixelcolor 1 2")]
    [InlineData("window activate Notepad")]
    [InlineData("clipboard set text")]
    [InlineData("shell \"printf ok\"")]
    [InlineData("delay 10")]
    [InlineData("set count 1")]
    [InlineData("inc count")]
    [InlineData("dec count")]
    [InlineData("mul count 2")]
    [InlineData("div count 2")]
    [InlineData("key A")]
    [InlineData("break")]
    [InlineData("continue")]
    [InlineData("}")]
    [InlineData("else {")]
    [InlineData("if count > 0 {")]
    [InlineData("while count > 0 {")]
    [InlineData("repeat 2 {")]
    [InlineData("for item in values {")]
    public void IsRuntimeStep_WhenRuntimeCommandOrBlockStep_ReturnsTrue(string step)
    {
        _ = RunScriptRuntimeStepClassifier.IsRuntimeStep(step).Should().BeTrue();
    }

    [Theory]
    [InlineData("pixelcolorish 1 2")]
    [InlineData("windowed activate Notepad")]
    [InlineData("clipboardish text")]
    [InlineData("shellfish command")]
    [InlineData("delayed 10")]
    [InlineData("setting count 1")]
    [InlineData("keyboard A")]
    [InlineData("break now")]
    [InlineData("continue now")]
    [InlineData("ifx count > 0 {")]
    [InlineData("plain text")]
    public void IsRuntimeStep_WhenCommandTokenIsNotDelimited_ReturnsFalse(string step)
    {
        _ = RunScriptRuntimeStepClassifier.IsRuntimeStep(step).Should().BeFalse();
    }

    [Fact]
    public void IsRuntimeStep_WhenStepIsNullOrWhitespace_ReturnsFalse()
    {
        _ = RunScriptRuntimeStepClassifier.IsRuntimeStep(null).Should().BeFalse();
        _ = RunScriptRuntimeStepClassifier.IsRuntimeStep(" \t ").Should().BeFalse();
    }
}
