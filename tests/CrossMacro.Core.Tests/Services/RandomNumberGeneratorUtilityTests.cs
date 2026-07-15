namespace CrossMacro.Core.Tests.Services;

public class RandomNumberGeneratorUtilityTests
{
    [Fact]
    public void GetInt32Inclusive_WhenBoundsAreEqual_ReturnsTheSingleton()
    {
        RandomNumberGeneratorUtility.GetInt32Inclusive(37, 37).Should().Be(37);
    }

    [Fact]
    public void GetInt32Inclusive_WhenBoundsAreReversed_ThrowsForMin()
    {
        var act = () => RandomNumberGeneratorUtility.GetInt32Inclusive(2, 1);

        act.Should().Throw<ArgumentOutOfRangeException>().Which.ParamName.Should().Be("min");
    }

    [Fact]
    public void GetInt32Inclusive_WhenRangeIsNegativeOnly_ReturnsWithinInclusiveRange()
    {
        var result = RandomNumberGeneratorUtility.GetInt32Inclusive(-100, -50);

        result.Should().BeInRange(-100, -50);
    }

    [Fact]
    public void GetInt32Inclusive_WhenRangeIsPositiveOnly_ReturnsWithinInclusiveRange()
    {
        var result = RandomNumberGeneratorUtility.GetInt32Inclusive(50, 100);

        result.Should().BeInRange(50, 100);
    }

    [Fact]
    public void GetInt32Inclusive_WhenRangeIsSingletonIntMax_ReturnsIntMaxValue()
    {
        RandomNumberGeneratorUtility.GetInt32Inclusive(int.MaxValue, int.MaxValue).Should().Be(int.MaxValue);
    }

    [Fact]
    public void GetInt32Inclusive_WhenRangeEndsAtIntMax_ReturnsWithinInclusiveRange()
    {
        var result = RandomNumberGeneratorUtility.GetInt32Inclusive(int.MaxValue - 1, int.MaxValue);

        result.Should().BeInRange(int.MaxValue - 1, int.MaxValue);
    }

    [Fact]
    public void GetInt32Inclusive_WhenRangeIsFullSignedIntRange_ReturnsWithinInclusiveRange()
    {
        var result = RandomNumberGeneratorUtility.GetInt32Inclusive(int.MinValue, int.MaxValue);

        result.Should().BeInRange(int.MinValue, int.MaxValue);
    }
}
