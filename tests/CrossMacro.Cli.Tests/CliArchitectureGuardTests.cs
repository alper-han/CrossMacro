
namespace CrossMacro.Cli.Tests;

public sealed class CliArchitectureGuardTests
{
    [Fact]
    public void CommandHandlers_ShouldNotDependOnCoreServicesDirectly()
    {
        var violations = CreateInspector().FindCoreServiceConstructorDependencies();

        Assert.True(
            violations.Length is 0,
            "Command handlers must depend on CLI service abstractions, not Core.Services directly."
            + Environment.NewLine
            + string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void CliLayer_ShouldNotTakeAvaloniaDependencies()
    {
        var violations = CreateInspector().FindAvaloniaReferences();

        Assert.True(
            violations.Length is 0,
            "CLI layer source must remain Avalonia-free."
            + Environment.NewLine
            + string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void CliCommandExecutor_ShouldNotDependOnRootServiceProvider()
    {
        var constructorParameters = CreateInspector().FindConstructorParameterTypes("CrossMacro.Cli.CliCommandExecutor");

        Assert.DoesNotContain("IServiceProvider", constructorParameters, StringComparer.Ordinal);
        Assert.Contains("ICliCommandHandlerResolver", constructorParameters, StringComparer.Ordinal);
    }

    [Fact]
    public void DoctorService_ShouldNotReferenceInfrastructureHelpersOrPathHelper()
    {
        var violations = CreateInspector().FindDoctorServiceForbiddenReferences();

        Assert.True(
            violations.Length is 0,
            "DoctorService must not reference Infrastructure helpers or PathHelper."
            + Environment.NewLine
            + string.Join(Environment.NewLine, violations));
    }

    private static CliSourceBoundaryInspector CreateInspector()
    {
        return CliSourceBoundaryInspector.FromRepositoryRoot(CliSourceBoundaryInspector.FindRepositoryRoot(AppContext.BaseDirectory));
    }
}
