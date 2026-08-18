
namespace CrossMacro.Core.Tests.Services;

public sealed class ScriptNumericExpressionTests
{
    private static readonly IReadOnlyDictionary<string, string> NoVariables =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    [Theory]
    [InlineData("5", "5", 5)]
    [InlineData("-5", "-5", -5)]
    [InlineData(" 42 ", "42", 42)]
    public void TryParse_WhenTokenIsIntegerLiteral_ParsesSimpleNumber(string token, string expectedLeftValue, int expectedValue)
    {
        var parsed = ScriptNumericExpression.TryParse(token, out var expression);

        _ = parsed.Should().BeTrue();
        _ = expression!.LeftSource.Should().Be(ScriptNumericSourceType.Number);
        _ = expression.LeftValue.Should().Be(expectedLeftValue);
        _ = expression.Op.Should().BeNull();

        var evaluated = ScriptNumericExpression.Evaluate(expression, NoVariables, out var value, out var error);

        _ = evaluated.Should().BeTrue();
        _ = error.Should().BeNull();
        _ = value.Should().Be(expectedValue);
    }

    [Fact]
    public void TryParse_WhenTokenIsVariableReference_ParsesSimpleVariable()
    {
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["a"] = "3",
        };

        var parsed = ScriptNumericExpression.TryParse("$a", out var expression);

        _ = parsed.Should().BeTrue();
        _ = expression!.LeftSource.Should().Be(ScriptNumericSourceType.VariableReference);
        _ = expression.LeftValue.Should().Be("$a");
        _ = expression.Op.Should().BeNull();

        var evaluated = ScriptNumericExpression.Evaluate(expression, variables, out var value, out var error);

        _ = evaluated.Should().BeTrue();
        _ = error.Should().BeNull();
        _ = value.Should().Be(3);
    }

    [Theory]
    [InlineData("5+3", 8)]
    [InlineData("5 + 3", 8)]
    [InlineData("10-4", 6)]
    [InlineData("6*7", 42)]
    [InlineData("10/3", 3)]
    [InlineData("7%3", 1)]
    [InlineData("5*-3", -15)]
    [InlineData("-5 - -3", -2)]
    public void TryParse_WhenTokenIsBinaryExpression_EvaluatesBothOperands(string token, int expectedValue)
    {
        var parsed = ScriptNumericExpression.TryParse(token, out var expression);

        _ = parsed.Should().BeTrue();
        _ = expression!.Op.Should().NotBeNull();

        var evaluated = ScriptNumericExpression.Evaluate(expression, NoVariables, out var value, out var error);

        _ = evaluated.Should().BeTrue();
        _ = error.Should().BeNull();
        _ = value.Should().Be(expectedValue);
    }

    [Theory]
    [InlineData("5+3", ScriptArithmeticOperation.Add)]
    [InlineData("5-3", ScriptArithmeticOperation.Subtract)]
    [InlineData("5*3", ScriptArithmeticOperation.Multiply)]
    [InlineData("5/3", ScriptArithmeticOperation.Divide)]
    [InlineData("5%3", ScriptArithmeticOperation.Modulo)]
    public void TryParse_WhenTokenIsBinaryExpression_MapsOperator(string token, ScriptArithmeticOperation expectedOp)
    {
        var parsed = ScriptNumericExpression.TryParse(token, out var expression);

        _ = parsed.Should().BeTrue();
        _ = expression!.Op.Should().Be(expectedOp);
        _ = expression.LeftSource.Should().Be(ScriptNumericSourceType.Number);
        _ = expression.RightSource.Should().Be(ScriptNumericSourceType.Number);
    }

    [Fact]
    public void Evaluate_WhenLeftOperandIsVariable_ResolvesAgainstVariables()
    {
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["a"] = "3",
        };

        var parsed = ScriptNumericExpression.TryParse("$a + 2", out var expression);

        _ = parsed.Should().BeTrue();
        _ = expression!.LeftSource.Should().Be(ScriptNumericSourceType.VariableReference);
        _ = expression.Op.Should().Be(ScriptArithmeticOperation.Add);
        _ = expression.RightSource.Should().Be(ScriptNumericSourceType.Number);

        var evaluated = ScriptNumericExpression.Evaluate(expression, variables, out var value, out var error);

        _ = evaluated.Should().BeTrue();
        _ = error.Should().BeNull();
        _ = value.Should().Be(5);
    }

    [Fact]
    public void Evaluate_WhenBothOperandsAreVariables_ResolvesBoth()
    {
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["a"] = "3",
            ["b"] = "4",
        };

        var parsed = ScriptNumericExpression.TryParse("$a + $b", out var expression);

        _ = parsed.Should().BeTrue();

        var evaluated = ScriptNumericExpression.Evaluate(expression!, variables, out var value, out var error);

        _ = evaluated.Should().BeTrue();
        _ = error.Should().BeNull();
        _ = value.Should().Be(7);
    }

    [Fact]
    public void Evaluate_WhenDividingByZero_ReturnsPinnedError()
    {
        var parsed = ScriptNumericExpression.TryParse("10/0", out var expression);

        _ = parsed.Should().BeTrue();

        var evaluated = ScriptNumericExpression.Evaluate(expression!, NoVariables, out _, out var error);

        _ = evaluated.Should().BeFalse();
        _ = error.Should().Be("Division by zero is not allowed in set expressions.");
    }

    [Fact]
    public void Evaluate_WhenModuloByZero_ReturnsPinnedError()
    {
        var parsed = ScriptNumericExpression.TryParse("10%0", out var expression);

        _ = parsed.Should().BeTrue();

        var evaluated = ScriptNumericExpression.Evaluate(expression!, NoVariables, out _, out var error);

        _ = evaluated.Should().BeFalse();
        _ = error.Should().Be("Modulo by zero is not allowed in set expressions.");
    }

    [Fact]
    public void Evaluate_WhenVariableIsUnknown_ReturnsError()
    {
        var parsed = ScriptNumericExpression.TryParse("$q", out var expression);

        _ = parsed.Should().BeTrue();

        var evaluated = ScriptNumericExpression.Evaluate(expression!, NoVariables, out _, out var error);

        _ = evaluated.Should().BeFalse();
        _ = error.Should().Be("Unknown variable '$q'.");
    }

    [Fact]
    public void Evaluate_WhenVariableValueIsNotInteger_ReturnsError()
    {
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["a"] = "foo",
        };

        var parsed = ScriptNumericExpression.TryParse("$a + 2", out var expression);

        _ = parsed.Should().BeTrue();

        var evaluated = ScriptNumericExpression.Evaluate(expression!, variables, out _, out var error);

        _ = evaluated.Should().BeFalse();
        _ = error.Should().Contain("$a");
    }

    [Theory]
    [InlineData("0x")]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("5-3-2")]
    [InlineData("1+2+3")]
    [InlineData("5 +")]
    [InlineData("+ 5")]
    [InlineData("- 5")]
    [InlineData("$5")]
    [InlineData("$$a")]
    public void TryParse_WhenTokenIsNotNumericExpression_ReturnsFalse(string token)
    {
        var parsed = ScriptNumericExpression.TryParse(token, out var expression);

        _ = parsed.Should().BeFalse();
        _ = expression.Should().BeNull();
    }

    [Theory]
    [InlineData("$count / 10", "$count / 10")]
    [InlineData("-5", "-5")]
    [InlineData("5 + $x", "5 + $x")]
    [InlineData("5+3", "5 + 3")]
    [InlineData("5*-3", "5 * -3")]
    [InlineData("$a", "$a")]
    [InlineData(" 7%3 ", "7 % 3")]
    public void Format_AfterTryParse_ProducesCanonicalToken(string token, string expectedCanonical)
    {
        var parsed = ScriptNumericExpression.TryParse(token, out var expression);

        _ = parsed.Should().BeTrue();
        _ = ScriptNumericExpression.Format(expression!).Should().Be(expectedCanonical);

        var reparsed = ScriptNumericExpression.TryParse(expectedCanonical, out var reparsedExpression);

        _ = reparsed.Should().BeTrue();
        _ = ScriptNumericExpression.Format(reparsedExpression!).Should().Be(expectedCanonical);
    }

    [Theory]
    [InlineData("5+3", 8)]
    [InlineData("$a + 2", 5)]
    public void Evaluate_WhenTokenIsValid_ReturnsEvaluatedStatus(string token, int expectedValue)
    {
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["a"] = "3",
        };

        var result = ScriptNumericExpression.Evaluate(token, variables);

        _ = result.Status.Should().Be(ScriptNumericExpressionStatus.Evaluated);
        _ = result.Value.Should().Be(expectedValue);
        _ = result.Error.Should().BeNull();
    }

    [Theory]
    [InlineData("- 5")]
    [InlineData("$a /")]
    [InlineData("+")]
    [InlineData("5 * - 3")]
    [InlineData("-$a")]
    [InlineData("5 +")]
    [InlineData("+ 5")]
    [InlineData("5-3-2")]
    [InlineData("1+2+3")]
    public void Evaluate_WhenTokenIsMalformed_ReturnsMalformedStatus(string token)
    {
        var result = ScriptNumericExpression.Evaluate(token, NoVariables);

        _ = result.Status.Should().Be(ScriptNumericExpressionStatus.Malformed);
        _ = result.Value.Should().Be(0);
        _ = result.Error.Should().Be($"'{token}' is not a valid numeric expression.");

        var parsed = ScriptNumericExpression.TryParse(token, out _);

        _ = parsed.Should().BeFalse();
    }

    [Theory]
    [InlineData("0x")]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("$5")]
    [InlineData("$$a")]
    [InlineData("hello-world")]
    [InlineData("99999999999999999999")]
    public void Evaluate_WhenTokenIsNotExpression_ReturnsNotExpressionStatus(string token)
    {
        var result = ScriptNumericExpression.Evaluate(token, NoVariables);

        _ = result.Status.Should().Be(ScriptNumericExpressionStatus.NotExpression);
        _ = result.Value.Should().Be(0);
        _ = result.Error.Should().BeNull();

        var parsed = ScriptNumericExpression.TryParse(token, out _);

        _ = parsed.Should().BeFalse();
    }

    [Theory]
    [InlineData("-5", ScriptNumericExpressionStatus.Evaluated, -5)]
    [InlineData("- 5", ScriptNumericExpressionStatus.Malformed, 0)]
    [InlineData("5*-3", ScriptNumericExpressionStatus.Evaluated, -15)]
    [InlineData("$a - 5", ScriptNumericExpressionStatus.Evaluated, -2)]
    [InlineData("5 * - 3", ScriptNumericExpressionStatus.Malformed, 0)]
    [InlineData("-$a", ScriptNumericExpressionStatus.Malformed, 0)]
    public void Evaluate_UnaryMinusMatrix_ClassifiesExplicitly(
        string token,
        ScriptNumericExpressionStatus expectedStatus,
        int expectedValue)
    {
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["a"] = "3",
        };

        var result = ScriptNumericExpression.Evaluate(token, variables);

        _ = result.Status.Should().Be(expectedStatus);
        _ = result.Value.Should().Be(expectedValue);
    }

    [Theory]
    [InlineData("-2147483648 / -1")]
    [InlineData("2147483647 + 1")]
    [InlineData("2000000000 * 3")]
    [InlineData("-2147483648 - 1")]
    public void Evaluate_WhenResultOverflowsInt_ReturnsOutOfRangeEvaluationError(string token)
    {
        var result = ScriptNumericExpression.Evaluate(token, NoVariables);

        _ = result.Status.Should().Be(ScriptNumericExpressionStatus.EvaluationError);
        _ = result.Error.Should().Be("Result is out of range for set expressions.");
    }

    [Theory]
    [InlineData("-2147483648 % -1", 0)]
    [InlineData("2147483647 + 0", 2147483647)]
    [InlineData("-2147483648 + 0", -2147483648)]
    [InlineData("2147483647", 2147483647)]
    [InlineData("-2147483648", -2147483648)]
    public void Evaluate_WhenResultFitsIntAtBoundary_ReturnsEvaluatedValue(string token, int expectedValue)
    {
        var result = ScriptNumericExpression.Evaluate(token, NoVariables);

        _ = result.Status.Should().Be(ScriptNumericExpressionStatus.Evaluated);
        _ = result.Value.Should().Be(expectedValue);
    }

    [Fact]
    public void Evaluate_BoolApi_WhenMinValueDividedByMinusOne_DoesNotThrowAndReturnsOutOfRange()
    {
        var parsed = ScriptNumericExpression.TryParse("-2147483648 / -1", out var expression);

        _ = parsed.Should().BeTrue();

        var evaluated = ScriptNumericExpression.Evaluate(expression!, NoVariables, out _, out var error);

        _ = evaluated.Should().BeFalse();
        _ = error.Should().Be("Result is out of range for set expressions.");
    }

    [Theory]
    [InlineData("10/0", "Division by zero is not allowed in set expressions.")]
    [InlineData("10%0", "Modulo by zero is not allowed in set expressions.")]
    public void Evaluate_WhenDivOrModByZeroWithoutContext_ReturnsLegacySetStrings(string token, string expectedError)
    {
        var result = ScriptNumericExpression.Evaluate(token, NoVariables);

        _ = result.Status.Should().Be(ScriptNumericExpressionStatus.EvaluationError);
        _ = result.Error.Should().Be(expectedError);
    }

    [Theory]
    [InlineData("10/0", "Division by zero is not allowed in repeat count.")]
    [InlineData("10%0", "Modulo by zero is not allowed in repeat count.")]
    [InlineData("- 5", "'- 5' is not a valid numeric expression for repeat count.")]
    [InlineData("2147483647 + 1", "Result is out of range for repeat count.")]
    public void Evaluate_WhenContextLabelProvided_InsertsLabelVerbatimInErrorText(string token, string expectedError)
    {
        var result = ScriptNumericExpression.Evaluate(token, NoVariables, "repeat count");

        _ = result.Error.Should().Be(expectedError);
        _ = result.Error.Should().Contain("repeat count");
    }

    [Fact]
    public void Evaluate_WhenVariableIsUnknown_ReturnsLegacyErrorText()
    {
        var result = ScriptNumericExpression.Evaluate("$q", NoVariables);

        _ = result.Status.Should().Be(ScriptNumericExpressionStatus.EvaluationError);
        _ = result.Error.Should().Be("Unknown variable '$q'.");
    }
}
