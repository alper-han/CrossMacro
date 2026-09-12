namespace CrossMacro.Mcp.Tests;

internal sealed class TestCliCommandHandlerResolver(ICliCommandHandler? handler = null) : ICliCommandHandlerResolver
{
    private readonly ICliCommandHandler? _handler = handler;

    public int ResolveCallCount { get; private set; }

    public ICliCommandHandler? Resolve(CliCommandOptions options)
    {
        ResolveCallCount++;
        return _handler;
    }
}
