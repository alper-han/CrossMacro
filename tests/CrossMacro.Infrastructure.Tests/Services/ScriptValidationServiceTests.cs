using CrossMacro.Infrastructure.Services;
using CrossMacro.Platform.Abstractions;
using FluentAssertions;
using NSubstitute;

namespace CrossMacro.Infrastructure.Tests.Services;

public sealed class ScriptValidationServiceTests
{
    [Fact]
    public void Validate_WhenScriptIsInvalid_ReturnsStableCategoryAndSource()
    {
        var service = new ScriptValidationService(Substitute.For<IKeyCodeMapper>());

        var diagnostics = service.Validate([new RunScriptStep("move abs", SourceLineNumber: 12, SourceIndex: 4)]);

        diagnostics.Should().ContainSingle();
        diagnostics[0].Category.Should().Be(ScriptValidationCategory.Compilation);
        diagnostics[0].SourceLineNumber.Should().Be(12);
        diagnostics[0].SourceIndex.Should().Be(4);
        diagnostics[0].Message.Should().Contain("Step");
    }

    [Fact]
    public void Validate_WhenScriptIsValid_ReturnsNoDiagnostics()
    {
        var service = new ScriptValidationService(Substitute.For<IKeyCodeMapper>());

        service.Validate([new RunScriptStep("click left")]).Should().BeEmpty();
    }
}
