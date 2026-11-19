namespace CrossMacro.Infrastructure.Tests.Services.Scripting;

public sealed class RunScriptVariablesTests
{
    [Theory]
    [InlineData("plain text", "plain text")]
    [InlineData("move rel $x $Y", "move rel 7 8")]
    [InlineData("cost $$x", "cost $x")]
    [InlineData("$x$y", "78")]
    [InlineData("$x_1", "$x_1")]
    public void TryResolveText_SubstitutesReferencesAndCollapsesEscapedDollars(string input, string expected)
    {
        var variables = new RunScriptVariables();
        variables.Set("x", "7");
        variables.Set("y", "8");
        variables.Set("x_1", "$x_1");

        var resolved = variables.TryResolveText(input, RunScriptDialect.Execution, out var text, out _);

        _ = resolved.Should().BeTrue();
        _ = text.Should().Be(expected);
    }

    [Fact]
    public void TryResolveText_WhenVariableIsUnknown_FlagsItSoCallersCanAddTheirOwnContext()
    {
        var variables = new RunScriptVariables();

        var resolved = variables.TryResolveText("go $missing", RunScriptDialect.Expansion, out _, out var error);

        _ = resolved.Should().BeFalse();
        _ = error.Message.Should().Be("Unknown variable '$missing'.");
        _ = error.IsUnknownVariable.Should().BeTrue();
    }

    [Theory]
    [InlineData("cost $", "Invalid variable reference '$'.", "Invalid variable name ''. Allowed pattern: [A-Za-z_][A-Za-z0-9_]*")]
    [InlineData("a $1x", "Invalid variable reference '$1'.", "Invalid variable name '1x'. Allowed pattern: [A-Za-z_][A-Za-z0-9_]*")]
    [InlineData("a $-", "Invalid variable reference '$-'.", "Invalid variable name ''. Allowed pattern: [A-Za-z_][A-Za-z0-9_]*")]
    public void TryResolveText_WhenReferenceIsMalformed_DescribesItInTheCallersDialect(string input, string expansionMessage, string executionMessage)
    {
        var variables = new RunScriptVariables();

        _ = variables.TryResolveText(input, RunScriptDialect.Expansion, out _, out var expansionError).Should().BeFalse();
        _ = variables.TryResolveText(input, RunScriptDialect.Execution, out _, out var executionError).Should().BeFalse();

        _ = expansionError.Message.Should().Be(expansionMessage);
        _ = executionError.Message.Should().Be(executionMessage);
        _ = expansionError.IsUnknownVariable.Should().BeFalse();
        _ = executionError.IsUnknownVariable.Should().BeFalse();
    }

    [Fact]
    public void Set_WritesThroughToTheScopedStore()
    {
        var store = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var variables = new RunScriptVariables(store);

        variables.Set("Total", "4");

        _ = store.Should().Contain("total", "4");
        _ = variables.ContainsKey("TOTAL").Should().BeTrue();
        _ = variables["total"].Should().Be("4");
    }
}
