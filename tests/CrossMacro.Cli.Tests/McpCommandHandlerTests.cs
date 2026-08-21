namespace CrossMacro.Cli.Tests;

public sealed class McpCommandHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldRunTheRegisteredMcpServerAndReturnSuccessAfterTheSessionEnds()
    {
        var server = Substitute.For<IMcpServer>();
        var handler = new McpCommandHandler(server);

        var result = await handler.ExecuteAsync(new McpCliOptions(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        await server.Received(1).RunAsync(CancellationToken.None, false);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldForwardRestrictedMode()
    {
        var server = Substitute.For<IMcpServer>();
        var handler = new McpCommandHandler(server);

        _ = await handler.ExecuteAsync(new McpCliOptions(Restricted: true), CancellationToken.None);

        await server.Received(1).RunAsync(CancellationToken.None, true);
    }
}
