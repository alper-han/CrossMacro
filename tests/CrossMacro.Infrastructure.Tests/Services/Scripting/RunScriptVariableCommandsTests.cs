using System.Globalization;

namespace CrossMacro.Infrastructure.Tests.Services.Scripting;

public sealed class RunScriptVariableCommandsTests
{
    private static VariableUpdateKind KindOf(string command) => command switch
    {
        "inc" => VariableUpdateKind.Increment,
        "dec" => VariableUpdateKind.Decrement,
        "mul" => VariableUpdateKind.Multiply,
        "div" => VariableUpdateKind.Divide,
        _ => throw new ArgumentOutOfRangeException(nameof(command), command, "Unknown command."),
    };

    [Theory]
    [InlineData("inc x", "inc", "x", "1")]
    [InlineData("DEC $x 3", "dec", "x", "3")]
    [InlineData("mul count 4", "mul", "count", "4")]
    [InlineData("div $count 2", "div", "count", "2")]
    public void TryParseUpdate_AcceptsOptionalDollarAndDefaultAmount(
        string step,
        string command,
        string name,
        string amount)
    {
        var handled = RunScriptVariableCommands.TryParseUpdate(step, out var update, out var error);

        _ = handled.Should().BeTrue();
        _ = error.Should().BeNull();
        _ = update.Should().Be(new VariableUpdate(KindOf(command), name, amount));
    }

    [Theory]
    [InlineData("inc")]
    [InlineData("click left")]
    [InlineData("set x 1")]
    public void TryParseUpdate_IgnoresOtherCommands(string step)
    {
        _ = RunScriptVariableCommands.TryParseUpdate(step, out _, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("inc x 1 2", "Invalid inc syntax. Expected: inc <name> [amount].")]
    [InlineData("mul x 1 1", "Invalid mul syntax. Expected: mul <name> [amount].")]
    [InlineData("inc 1bad", "Invalid variable name '1bad'. Allowed pattern: [A-Za-z_][A-Za-z0-9_]*")]
    [InlineData("dec $$x", "Invalid variable name '$$x'. Allowed pattern: [A-Za-z_][A-Za-z0-9_]*")]
    public void TryParseUpdate_ReportsMalformedCommands(string step, string expectedError)
    {
        var handled = RunScriptVariableCommands.TryParseUpdate(step, out _, out var error);

        _ = handled.Should().BeTrue();
        _ = error.Should().Be(expectedError);
    }

    [Theory]
    [InlineData("set x 5", "x", "5")]
    [InlineData("set $x=5", "x", "5")]
    [InlineData("set name = hello world", "name", "hello world")]
    public void TryParseAssignment_AcceptsBothForms(string step, string name, string value)
    {
        var handled = RunScriptVariableCommands.TryParseAssignment(step, out var assignment, out var error);

        _ = handled.Should().BeTrue();
        _ = error.Should().BeNull();
        _ = assignment.Should().Be(new VariableAssignment(name, value));
    }

    [Theory]
    [InlineData("set x", "Invalid set syntax. Expected: set <name> <value> or set <name>=<value>.")]
    [InlineData("set x=", "Variable 'x' value cannot be empty.")]
    [InlineData("set 1x 3", "Invalid variable name '1x'. Allowed pattern: [A-Za-z_][A-Za-z0-9_]*")]
    public void TryParseAssignment_ReportsMalformedCommands(string step, string expectedError)
    {
        var handled = RunScriptVariableCommands.TryParseAssignment(step, out _, out var error);

        _ = handled.Should().BeTrue();
        _ = error.Should().Be(expectedError);
    }

    [Theory]
    [InlineData("inc", int.MaxValue, 1, "Result is out of range for inc/dec.")]
    [InlineData("dec", int.MinValue, 1, "Result is out of range for inc/dec.")]
    [InlineData("mul", int.MaxValue, 2, "Result is out of range for mul/div.")]
    [InlineData("div", int.MinValue, -1, "Result is out of range for mul/div.")]
    [InlineData("div", 10, 0, "Division by zero is not allowed in mul/div.")]
    public void TryApply_RejectsOverflowAndDivisionByZero(string command, int current, int amount, string expectedError)
    {
        var update = new VariableUpdate(KindOf(command), "x", amount.ToString(CultureInfo.InvariantCulture));

        var applied = RunScriptVariableCommands.TryApply(update, current, amount, out _, out var error);

        _ = applied.Should().BeFalse();
        _ = error.Should().Be(expectedError);
    }

    [Theory]
    [InlineData("inc", 7, 3, 10)]
    [InlineData("dec", 7, 3, 4)]
    [InlineData("mul", 7, 3, 21)]
    [InlineData("div", 7, 2, 3)]
    [InlineData("inc", int.MaxValue - 1, 1, int.MaxValue)]
    public void TryApply_ComputesInRange(string command, int current, int amount, int expected)
    {
        var update = new VariableUpdate(KindOf(command), "x", amount.ToString(CultureInfo.InvariantCulture));

        var applied = RunScriptVariableCommands.TryApply(update, current, amount, out var result, out var error);

        _ = applied.Should().BeTrue();
        _ = error.Should().BeNull();
        _ = result.Should().Be(expected);
    }

    [Fact]
    public void TryReadInteger_WhenVariableMissing_NamesTheCommandFamily()
    {
        var update = new VariableUpdate(VariableUpdateKind.Multiply, "x", "2");

        var read = RunScriptVariableCommands.TryReadInteger(update, currentValue: null, out _, out var error);

        _ = read.Should().BeFalse();
        _ = error.Should().Be("variable 'x' must exist and be an integer for mul/div.");
    }
}
