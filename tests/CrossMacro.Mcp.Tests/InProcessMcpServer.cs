namespace CrossMacro.Mcp.Tests;

/// <summary>An MCP server and connected client joined by in-memory pipes, so tests exercise the real protocol path.</summary>
internal sealed class InProcessMcpServer : IAsyncDisposable
{
    private readonly ServiceProvider _provider;
    private readonly Task _serverTask;
    private readonly CancellationTokenSource _cancellation;

    private InProcessMcpServer(ServiceProvider provider, Task serverTask, McpClient client, CancellationTokenSource cancellation)
    {
        _provider = provider;
        _serverTask = serverTask;
        Client = client;
        _cancellation = cancellation;
    }

    public McpClient Client { get; }

    public CancellationToken Token => _cancellation.Token;

    public static async Task<InProcessMcpServer> StartAsync(
        Action<IMcpServerBuilder> configureServer,
        Action<IServiceCollection>? registerServices = null)
    {
        var clientToServer = new Pipe();
        var serverToClient = new Pipe();
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromSeconds(10));

        var services = new ServiceCollection();
        registerServices?.Invoke(services);
        var builder = services
            .AddMcpServer(options => options.ProtocolVersion = "2026-07-28")
            .WithStreamServerTransport(clientToServer.Reader.AsStream(), serverToClient.Writer.AsStream());
        configureServer(builder);

        var provider = services.BuildServiceProvider(validateScopes: true);
        var serverTask = provider.GetRequiredService<McpServer>().RunAsync(cancellation.Token);
        var client = await McpClient.CreateAsync(
            new StreamClientTransport(clientToServer.Writer.AsStream(), serverToClient.Reader.AsStream()),
            cancellationToken: cancellation.Token);
        return new InProcessMcpServer(provider, serverTask, client, cancellation);
    }

    public async ValueTask DisposeAsync()
    {
        await _cancellation.CancelAsync();
        await _serverTask;
        await Client.DisposeAsync();
        await _provider.DisposeAsync();
        _cancellation.Dispose();
    }
}
