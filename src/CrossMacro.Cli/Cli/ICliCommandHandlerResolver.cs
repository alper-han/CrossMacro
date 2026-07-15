namespace CrossMacro.Cli;

public interface ICliCommandHandlerResolver
{
    public ICliCommandHandler? Resolve(CliCommandOptions options);
}
