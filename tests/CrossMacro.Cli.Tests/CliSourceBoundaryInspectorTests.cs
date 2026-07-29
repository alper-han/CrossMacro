namespace CrossMacro.Cli.Tests;

public sealed class CliSourceBoundaryInspectorTests
{
    [Fact]
    public void FindCoreServiceConstructorDependencies_ReportsDirectQualifiedAndNestedGenericDependencies()
    {
        var inspector = CreateInspector(
            ("src/CrossMacro.Core/Services/Contracts.cs", "namespace CrossMacro.Core.Services; public interface ICoreService {} public interface IAnotherService {}"),
            ("src/CrossMacro.Cli/Commands/Handlers.cs", """
                using CrossMacro.Core.Services;
                namespace CrossMacro.Cli.Commands;
                public abstract class HandlerBase : ICliCommandHandler {}
                public sealed class Direct(ICoreService service) : HandlerBase {}
                public sealed class Qualified(global::CrossMacro.Core.Services.IAnotherService service) : HandlerBase {}
                public sealed class Nested(System.Collections.Generic.IReadOnlyList<ICoreService> services) : HandlerBase {}
                public sealed class MemberOnly : HandlerBase { private ICoreService? Service { get; } }
                """));

        var violations = inspector.FindCoreServiceConstructorDependencies();

        Assert.Equal(3, violations.Length);
        Assert.Contains(violations, violation => violation.Contains("Direct -> ICoreService", StringComparison.Ordinal));
        Assert.Contains(violations, violation => violation.Contains("Qualified -> global::CrossMacro.Core.Services.IAnotherService", StringComparison.Ordinal));
        Assert.Contains(violations, violation => violation.Contains("Nested -> System.Collections.Generic.IReadOnlyList<ICoreService>", StringComparison.Ordinal));
        Assert.DoesNotContain(violations, violation => violation.Contains("MemberOnly", StringComparison.Ordinal));
    }

    [Fact]
    public void FindCoreServiceConstructorDependencies_RecognizesTransitiveHandlerInheritance()
    {
        var inspector = CreateInspector(
            ("src/CrossMacro.Core/Services/Contracts.cs", "namespace CrossMacro.Core.Services; public interface ICoreService {}"),
            ("src/CrossMacro.Cli/Commands/Handlers.cs", """
                using CrossMacro.Core.Services;
                namespace CrossMacro.Cli.Commands;
                public abstract class First : ICliCommandHandler {}
                public abstract class Second : First {}
                public sealed class Concrete(ICoreService service) : Second {}
                """));

        var violations = inspector.FindCoreServiceConstructorDependencies();

        var violation = Assert.Single(violations);
        Assert.Contains("Concrete -> ICoreService", violation, StringComparison.Ordinal);
    }

    [Fact]
    public void FindAvaloniaReferences_ReportsSyntaxButIgnoresCommentsAndStringLiterals()
    {
        var inspector = CreateInspector(("src/CrossMacro.Cli/References.cs", """
            global using Avalonia.Controls;
            using Avalonia.Input;
            using Av = Avalonia.Media;
            namespace CrossMacro.Cli;
            // Avalonia.Input should not count.
            public sealed class References { private global::Avalonia.Controls.Window? _window; private string Text = "Avalonia.Controls.Window"; }
            """));

        var violations = inspector.FindAvaloniaReferences();

        Assert.Equal(4, violations.Length);
        Assert.Contains(violations, violation => violation.Contains("Avalonia.Controls", StringComparison.Ordinal));
        Assert.Contains(violations, violation => violation.Contains("Avalonia.Input", StringComparison.Ordinal));
        Assert.Contains(violations, violation => violation.Contains("Avalonia.Media", StringComparison.Ordinal));
        Assert.Contains(violations, violation => violation.Contains("global::Avalonia.Controls.Window", StringComparison.Ordinal));
    }

    [Fact]
    public void FindConstructorParameterTypes_ExtractsPrimaryConstructorParameters()
    {
        var inspector = CreateInspector(("src/CrossMacro.Cli/CliCommandExecutor.cs", """
            namespace CrossMacro.Cli;
            public sealed class CliCommandExecutor(ICliCommandHandlerResolver handlerResolver, IServiceProvider serviceProvider) {}
            """));

        var parameters = inspector.FindConstructorParameterTypes("CrossMacro.Cli.CliCommandExecutor");

        Assert.Equal(["ICliCommandHandlerResolver", "IServiceProvider"], parameters);
    }

    private static CliSourceBoundaryInspector CreateInspector(params (string Path, string Text)[] documents)
    {
        return new CliSourceBoundaryInspector(documents.Select(document => new CliSourceBoundaryInspector.SourceDocument(document.Path, document.Text)));
    }
}
