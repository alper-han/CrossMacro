
namespace CrossMacro.Infrastructure.Tests.Services;

public sealed class EditorActionValidatorScriptStateTests
{
    private readonly EditorActionValidator _validator = new(
        new EditorActionConverter(Substitute.For<IKeyCodeMapper>()));

    [Theory]
    [InlineData(EditorActionType.MultiplyVariable)]
    [InlineData(EditorActionType.DivideVariable)]
    public void Validate_MulDivWithNumberOrVariableAmount_ReturnsValid(EditorActionType actionType)
    {
        var numberAmount = new EditorAction
        {
            Type = actionType,
            ScriptVariableName = "x",
            ScriptNumericSourceType = ScriptNumericSourceType.Number,
            ScriptNumericValue = "2",
        };
        var variableAmount = new EditorAction
        {
            Type = actionType,
            ScriptVariableName = "x",
            ScriptNumericSourceType = ScriptNumericSourceType.VariableReference,
            ScriptNumericValue = "$factor",
        };

        _ = _validator.Validate(numberAmount).IsValid.Should().BeTrue();
        _ = _validator.Validate(variableAmount).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(EditorActionType.MultiplyVariable, "1bad")]
    [InlineData(EditorActionType.DivideVariable, "$ x")]
    public void Validate_MulDivWithInvalidVariableName_ReturnsInvalid(EditorActionType actionType, string variableName)
    {
        var action = new EditorAction
        {
            Type = actionType,
            ScriptVariableName = variableName,
            ScriptNumericSourceType = ScriptNumericSourceType.Number,
            ScriptNumericValue = "2",
        };

        _ = _validator.Validate(action).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(EditorActionType.MultiplyVariable, ScriptNumericSourceType.Number, "abc")]
    [InlineData(EditorActionType.DivideVariable, ScriptNumericSourceType.Number, "")]
    [InlineData(EditorActionType.MultiplyVariable, ScriptNumericSourceType.VariableReference, "1bad")]
    [InlineData(EditorActionType.DivideVariable, ScriptNumericSourceType.VariableReference, "has space")]
    [InlineData(EditorActionType.IncrementVariable, ScriptNumericSourceType.Number, "1 + 2")]
    public void Validate_AmountThatIsNotAPlainNumberOrVariable_ReturnsInvalid(
        EditorActionType actionType,
        ScriptNumericSourceType sourceType,
        string amount)
    {
        var action = new EditorAction
        {
            Type = actionType,
            ScriptVariableName = "x",
            ScriptNumericSourceType = sourceType,
            ScriptNumericValue = amount,
        };

        _ = _validator.Validate(action).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("$x", true)]
    [InlineData("-15", true)]
    [InlineData("$1bad", false)]
    [InlineData("$ x", false)]
    [InlineData("$x y", false)]
    [InlineData("not-a-coordinate", false)]
    public void Validate_MouseMoveCoordinateTokens_AcceptsOnlyIntegersAndVariables(string xToken, bool expected)
    {
        var action = new EditorAction
        {
            Type = EditorActionType.MouseMove,
            CoordinateXToken = xToken,
            CoordinateYToken = "1",
            IsAbsolute = false,
        };

        _ = _validator.Validate(action).IsValid.Should().Be(expected);
    }

    [Theory]
    [InlineData("selectedText", true)]
    [InlineData("1invalid", false)]
    public void Validate_CopySelectionToVariable_RequiresValidDestination(string variableName, bool expected)
    {
        var action = new EditorAction
        {
            Type = EditorActionType.CopySelectionToVariable,
            ClipboardCopyShortcut = ClipboardCopyShortcut.CtrlShiftC,
            ScriptVariableName = variableName,
        };

        _ = _validator.Validate(action).IsValid.Should().Be(expected);
    }

    [Fact]
    public void Validate_WindowCommandWithUndefinedMode_ReturnsInvalid()
    {
        var action = new EditorAction
        {
            Type = EditorActionType.WindowCommand,
            WindowCommandMode = (WindowCommandMode)999,
        };

        _ = _validator.Validate(action).IsValid.Should().BeFalse();
    }
}
