namespace CrossMacro.Cli;

internal sealed record CliCommandHandlerRegistration(
    Type OptionsType,
    Func<ICliCommandHandler> CreateHandler);
