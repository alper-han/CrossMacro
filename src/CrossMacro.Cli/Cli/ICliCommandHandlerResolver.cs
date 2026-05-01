namespace CrossMacro.Cli;

public interface ICliCommandHandlerResolver
{
    ICliCommandHandler? Resolve(CliCommandOptions options);
}
