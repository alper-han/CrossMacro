namespace CrossMacro.Cli.Tests;

public sealed class McpCommandHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldRunTheRegisteredMcpServerAndReturnSuccessAfterTheSessionEnds()
    {
        var server = Substitute.For<IMcpServer>();
        var handler = new McpCommandHandler(server);

        var result = await handler.ExecuteAsync(new McpOptions(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(ExecutionOutcomeCode.Success, result.Outcome);
        await server.Received(1).RunAsync(cancellationToken: CancellationToken.None, restricted: false);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldForwardRestrictedMode()
    {
        var server = Substitute.For<IMcpServer>();
        var handler = new McpCommandHandler(server);

        _ = await handler.ExecuteAsync(new McpOptions(Restricted: true), CancellationToken.None);

        await server.Received(1).RunAsync(cancellationToken: CancellationToken.None, restricted: true);
    }
}
