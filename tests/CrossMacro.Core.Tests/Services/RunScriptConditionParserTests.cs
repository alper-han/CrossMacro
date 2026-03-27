using CrossMacro.Core.Services;

namespace CrossMacro.Core.Tests.Services;

public class RunScriptConditionParserTests
{
    [Fact]
    public void TryParse_WhenRightOperandContainsComparatorText_ParsesEqualityBoundary()
    {
        // Act
        var success = RunScriptConditionParser.TryParse("$mode == a>=b", out var condition, out var error);

        // Assert
        Assert.True(success);
        Assert.Null(error);
        Assert.NotNull(condition);
        Assert.Equal("$mode", condition!.LeftToken);
        Assert.Equal("==", condition.OperatorToken);
        Assert.Equal("a>=b", condition.RightToken);
    }

    [Fact]
    public void TryParse_WhenOperatorMissing_ReturnsError()
    {
        // Act
        var success = RunScriptConditionParser.TryParse("$mode equals fast", out var condition, out var error);

        // Assert
        Assert.False(success);
        Assert.Null(condition);
        Assert.Equal("Unsupported condition operator. Allowed: ==, !=, >, >=, <, <=.", error);
    }

    [Fact]
    public void TryParse_WhenBoundaryIsInvalid_ReturnsBoundaryError()
    {
        // Act
        var success = RunScriptConditionParser.TryParse(">= 10", out var condition, out var error);

        // Assert
        Assert.False(success);
        Assert.Null(condition);
        Assert.Equal("Condition must be in the form: <left> <op> <right>.", error);
    }
}
