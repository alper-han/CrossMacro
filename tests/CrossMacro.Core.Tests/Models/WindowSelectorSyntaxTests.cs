namespace CrossMacro.Core.Tests.Models;

public sealed class WindowSelectorSyntaxTests
{
    private static readonly string[] ActiveTokens = ["title", "class", "address", "fullscreen", "maximize", "float", "pinned", "hidden", "geometry"];
    [Theory]
    [InlineData(WindowCommandMode.Search, "class", true)]
    [InlineData(WindowCommandMode.Wait, "address", false)]
    [InlineData(WindowCommandMode.Focus, "class", true)]
    [InlineData(WindowCommandMode.Close, "class", false)]
    [InlineData(WindowCommandMode.Close, "active", true)]
    [InlineData(WindowCommandMode.Focus, "TITLE", false)]
    [InlineData(WindowCommandMode.Focus, " title ", false)]
    [InlineData(WindowCommandMode.Resize, "title", false)]
    public void SelectorPolicy_PreservesCanonicalSpellingAndCommandBoundaries(WindowCommandMode mode, string token, bool allowed)
    {
        Assert.Equal(allowed, WindowSelectorSyntax.IsAllowed(mode, token));
    }

    [Fact]
    public void ActiveFieldOptions_PreserveBindingTokensAndOrder()
    {
        Assert.Equal(ActiveTokens, WindowActiveFieldSyntax.Tokens);
        foreach (var token in WindowActiveFieldSyntax.Tokens)
        {
            Assert.True(WindowActiveFieldSyntax.TryParse(token, out var field));
            Assert.Equal(token, WindowActiveFieldSyntax.Format(field));
        }
    }

    [Theory]
    [InlineData("==", ScriptConditionOperator.Equals)]
    [InlineData("!=", ScriptConditionOperator.NotEquals)]
    [InlineData(">", ScriptConditionOperator.GreaterThan)]
    [InlineData(">=", ScriptConditionOperator.GreaterThanOrEqual)]
    [InlineData("<", ScriptConditionOperator.LessThan)]
    [InlineData("<=", ScriptConditionOperator.LessThanOrEqual)]
    public void ConditionOperators_RoundTripWithoutChangingScriptSpelling(string token, ScriptConditionOperator expected)
    {
        Assert.True(ScriptConditionOperatorSyntax.TryParse(token, out var operation));
        Assert.Equal(expected, operation);
        Assert.Equal(token, ScriptConditionOperatorSyntax.Format(operation));
        Assert.False(ScriptConditionOperatorSyntax.TryParse("===", out _));
    }
}
