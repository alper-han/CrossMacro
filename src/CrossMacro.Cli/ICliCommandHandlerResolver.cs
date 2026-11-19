namespace CrossMacro.Cli;

public interface ICliCommandHandlerResolver
{
    public ICliCommandHandler? Resolve(CommandOptions options);
}
