
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
}
