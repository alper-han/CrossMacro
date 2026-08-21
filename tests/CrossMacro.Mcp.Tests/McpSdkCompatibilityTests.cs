namespace CrossMacro.Mcp.Tests;

public sealed class McpSdkCompatibilityTests
{
    private const string ProbeResponse = "ok";
    private readonly string _probeResponse = ProbeResponse;

    [Fact]
    public async Task GenericToolRegistration_ShouldServeAProbeToolOverTheModernStreamProtocol()
    {
        var clientToServer = new Pipe();
        var serverToClient = new Pipe();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var services = new ServiceCollection();
        _ = services
            .AddMcpServer(options => options.ProtocolVersion = "2026-07-28")
            .WithStreamServerTransport(
                clientToServer.Reader.AsStream(),
                serverToClient.Writer.AsStream())
            .WithTools<McpSdkCompatibilityTests>();

        await using var provider = services.BuildServiceProvider(validateScopes: true);
        var server = provider.GetRequiredService<McpServer>();
        var serverTask = server.RunAsync(cancellation.Token);

        await using var client = await McpClient.CreateAsync(
            new StreamClientTransport(
                clientToServer.Writer.AsStream(),
                serverToClient.Reader.AsStream()),
            cancellationToken: cancellation.Token);

        var tool = Assert.Single(await client.ListToolsAsync(cancellationToken: cancellation.Token));
        Assert.Equal("sdk.probe", tool.Name);

        await cancellation.CancelAsync();
        await serverTask;
    }
    [McpServerTool(Name = "sdk.probe", ReadOnly = true, Destructive = false, Idempotent = true)]
    public string Probe() => _probeResponse;
}
