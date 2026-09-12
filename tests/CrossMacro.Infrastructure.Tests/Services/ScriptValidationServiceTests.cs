
namespace CrossMacro.Infrastructure.Tests.Services;

public sealed class ScriptValidationServiceTests
{
    [Fact]
    public void Validate_WhenScriptIsInvalid_ReturnsStableCategoryAndSource()
    {
        var service = new ScriptValidationService(Substitute.For<IKeyCodeMapper>());

        var diagnostics = service.Validate([new RunScriptStep("move abs", SourceLineNumber: 12, SourceIndex: 4)]);

        _ = diagnostics.Should().ContainSingle();
        _ = diagnostics[0].Category.Should().Be(ScriptValidationCategory.Compilation);
        _ = diagnostics[0].SourceLineNumber.Should().Be(12);
        _ = diagnostics[0].SourceIndex.Should().Be(4);
        _ = diagnostics[0].Message.Should().Contain("Step");
    }

    [Fact]
    public void Validate_WhenScriptIsValid_ReturnsNoDiagnostics()
    {
        var service = new ScriptValidationService(Substitute.For<IKeyCodeMapper>());

        _ = service.Validate([new RunScriptStep("click left")]).Should().BeEmpty();
    }

    [Fact]
    public void Validate_WhenStepsAreEmpty_ReturnsNoDiagnostics()
    {
        var service = new ScriptValidationService(Substitute.For<IKeyCodeMapper>());

        _ = service.Validate([]).Should().BeEmpty();
    }

    [Fact]
    public void Validate_WhenStepsAreNull_ThrowsArgumentNullException()
    {
        var service = new ScriptValidationService(Substitute.For<IKeyCodeMapper>());

        var act = () => service.Validate(null!);

        _ = act.Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("steps");
    }

    [Fact]
    public void Constructor_WhenKeyCodeMapperIsNull_ThrowsArgumentNullException()
    {
        var act = () => new ScriptValidationService(null!);

        _ = act.Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("keyCodeMapper");
    }
}
