using System;
using System.Linq;
using System.Reflection;
using CrossMacro.Cli;

namespace CrossMacro.Cli.Tests;

public class CliArchitectureGuardTests
{
    [Fact]
    public void CommandHandlers_ShouldNotDependOnCoreServicesDirectly()
    {
        var handlerTypes = typeof(ICliCommandHandler).Assembly
            .GetTypes()
            .Where(x =>
                x is { IsClass: true, IsAbstract: false } &&
                x.Namespace == "CrossMacro.Cli.Commands" &&
                typeof(ICliCommandHandler).IsAssignableFrom(x))
            .ToArray();

        var violations = handlerTypes
            .SelectMany(handlerType => handlerType
                .GetConstructors()
                .SelectMany(ctor => ctor.GetParameters().Select(p => (handlerType, dependencyType: p.ParameterType))))
            .Where(x => x.dependencyType.Namespace?.StartsWith("CrossMacro.Core.Services", StringComparison.Ordinal) == true)
            .Select(x => $"{x.handlerType.Name} -> {x.dependencyType.FullName}")
            .OrderBy(x => x)
            .ToArray();

        Assert.True(
            violations.Length == 0,
            "Command handlers must depend on CLI service abstractions, not Core.Services directly."
            + Environment.NewLine
            + string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void CliLayer_ShouldNotTakeAvaloniaDependenciesInConstructors()
    {
        var cliTypes = typeof(ICliCommandHandler).Assembly
            .GetTypes()
            .Where(x =>
                x is { IsClass: true, IsAbstract: false } &&
                x.Namespace != null &&
                x.Namespace.StartsWith("CrossMacro.Cli", StringComparison.Ordinal))
            .ToArray();

        var violations = cliTypes
            .SelectMany(type => type
                .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .SelectMany(ctor => ctor.GetParameters().Select(p => (type, dependencyType: p.ParameterType))))
            .Where(x => HasAvaloniaType(x.dependencyType))
            .Select(x => $"{x.type.FullName} -> {x.dependencyType.FullName}")
            .OrderBy(x => x)
            .ToArray();

        Assert.True(
            violations.Length == 0,
            "CLI layer constructors must remain Avalonia-free."
            + Environment.NewLine
            + string.Join(Environment.NewLine, violations));
    }

    private static bool HasAvaloniaType(Type type)
    {
        if (type.Namespace?.StartsWith("Avalonia", StringComparison.Ordinal) == true)
        {
            return true;
        }

        if (type.IsArray)
        {
            return HasAvaloniaType(type.GetElementType()!);
        }

        if (!type.IsGenericType)
        {
            return false;
        }

        return type.GetGenericArguments().Any(HasAvaloniaType);
    }
}
