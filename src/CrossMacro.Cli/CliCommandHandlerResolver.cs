
using System.Collections.Frozen;

namespace CrossMacro.Cli;

public sealed class CliCommandHandlerResolver : ICliCommandHandlerResolver
{
    private readonly FrozenDictionary<Type, Func<ICliCommandHandler>> _handlersByOptionsType;

    internal CliCommandHandlerResolver(IEnumerable<CliCommandHandlerRegistration> registrations)
    {
        var registrationsArray = registrations.ToArray();
        var duplicateRegistration = registrationsArray
            .GroupBy(static registration => registration.OptionsType)
            .FirstOrDefault(static registrationsByOptionsType => registrationsByOptionsType.Skip(1).Any());
        if (duplicateRegistration is not null)
        {
            throw new InvalidOperationException(
                $"More than one CLI command handler is registered for options type: {duplicateRegistration.Key.FullName}");
        }

        _handlersByOptionsType = registrationsArray.ToFrozenDictionary(
            static registration => registration.OptionsType,
            static registration => registration.CreateHandler);
    }

    public ICliCommandHandler? Resolve(CliCommandOptions? options)
    {
        if (options is null)
        {
            return null;
        }

        return _handlersByOptionsType.GetValueOrDefault(options.GetType())?.Invoke();
    }
}
