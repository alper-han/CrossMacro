namespace CrossMacro.Core.Tests.Architecture;

public sealed class SourceArchitectureInspectorTests
{
    [Fact]
    public void References_IgnoreDocumentationAndStringContents_ButRetainExpressions()
    {
        const string source = "// CrossMacro.Infrastructure\nclass Example { string Text = \"Environment.GetFolderPath\"; }";
        Assert.Empty(SourceArchitectureInspector.FindReferences(source, ["CrossMacro.Infrastructure", "Environment.GetFolderPath"]));
        _ = Assert.Single(SourceArchitectureInspector.FindReferences("class Example { string Path => Environment . GetFolderPath(0); }", ["Environment.GetFolderPath"]));
    }

    [Fact]
    public void TypeOwnership_UsesNamespaceNestingAndGenericArity()
    {
        var types = SourceArchitectureInspector.DeclaredTypes("namespace Feature { partial class Item<T> { class State {} } } ");
        Assert.Equal(["Feature.Item`1", "Feature.Item`1.State"], types, StringComparer.Ordinal);
        Assert.Empty(SourceArchitectureInspector.DeclaredTypes("file class Local {}"));
    }
}
