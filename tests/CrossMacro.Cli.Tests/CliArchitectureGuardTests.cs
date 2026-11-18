
namespace CrossMacro.Cli.Tests;

public sealed class CliArchitectureGuardTests
{
    [Fact]
    public void CommandHandlers_ShouldNotDependOnCoreServicesDirectly()
    {
        var violations = typeof(ICliCommandHandler).Assembly.GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false } && typeof(ICliCommandHandler).IsAssignableFrom(type))
            .SelectMany(handler => handler.GetConstructors()
                .SelectMany(constructor => constructor.GetParameters())
                .Where(parameter => ReferencesCoreService(parameter.ParameterType))
                .Select(parameter => $"{handler.FullName} -> {parameter.ParameterType}"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            violations.Length is 0,
            "Command handlers must depend on CLI service abstractions, not Core.Services directly."
            + Environment.NewLine
            + string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void CliCommandExecutor_ShouldOnlyDependOnTheHandlerResolver()
    {
        var parameters = typeof(CliCommandExecutor).GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .Select(parameter => parameter.ParameterType)
            .ToArray();

        Assert.Equal([typeof(ICliCommandHandlerResolver)], parameters);
    }

    private static bool ReferencesCoreService(Type type)
    {
        return (type.Namespace?.StartsWith("CrossMacro.Core.Services", StringComparison.Ordinal) ?? false)
            || (type.HasElementType && ReferencesCoreService(type.GetElementType()!))
            || type.GenericTypeArguments.Any(ReferencesCoreService);
    }
}
