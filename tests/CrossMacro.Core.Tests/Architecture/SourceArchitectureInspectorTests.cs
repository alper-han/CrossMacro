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
}
