namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;

using CrossMacro.Platform.Linux.DisplayServer.Wayland;

public class WayfirePositionProviderTests
{
    private const string CursorMethod = "window-rules/get_cursor_position";
    private const string OutputsMethod = "window-rules/list-outputs";

    [Fact]
    public void Constructor_ShouldSetUnsupported_WhenRequiredMethodsAreUnavailable()
    {
        var ipcClient = new FakeWayfireIpcClient { IsAvailable = true };
        ipcClient.Enqueue(CursorMethod, "{\"error\":\"No such method found!\"}");

        using var provider = new WayfirePositionProvider(ipcClient);

        Assert.False(provider.IsSupported);
    }

    [Fact]
    public async Task GetAbsolutePositionAsync_ShouldNormalizeUsingLayoutOrigin()
    {
        var ipcClient = new FakeWayfireIpcClient { IsAvailable = true };
        ipcClient.Enqueue(CursorMethod, "{\"pos\":{\"x\":1200.0,\"y\":300.0}}"); // capability probe
        ipcClient.Enqueue(OutputsMethod, OutputsWithNegativeOrigin());
        ipcClient.Enqueue(CursorMethod, "{\"pos\":{\"x\":-100.0,\"y\":200.0}}"); // runtime read

        using var provider = new WayfirePositionProvider(ipcClient);
        var position = await provider.GetAbsolutePositionAsync();

        Assert.True(provider.IsSupported);
        Assert.Equal((1820, 200), position);
    }

    [Fact]
    public async Task GetScreenResolutionAsync_ShouldReturnUnionOfOutputs()
    {
        var ipcClient = new FakeWayfireIpcClient { IsAvailable = true };
        ipcClient.Enqueue(CursorMethod, "{\"pos\":{\"x\":0.0,\"y\":0.0}}"); // capability probe
        ipcClient.Enqueue(OutputsMethod, OutputsWithNegativeOrigin());
        ipcClient.Enqueue(OutputsMethod, OutputsWithNegativeOrigin()); // explicit resolution call

        using var provider = new WayfirePositionProvider(ipcClient);
        var resolution = await provider.GetScreenResolutionAsync();

        Assert.Equal((4480, 1440), resolution);
    }

    [Fact]
    public async Task GetAbsolutePositionAsync_ShouldDisableProvider_WhenMethodBecomesUnavailable()
    {
        var ipcClient = new FakeWayfireIpcClient { IsAvailable = true };
        ipcClient.Enqueue(CursorMethod, "{\"pos\":{\"x\":0.0,\"y\":0.0}}"); // capability probe
        ipcClient.Enqueue(OutputsMethod, OutputsWithNegativeOrigin());
        ipcClient.Enqueue(CursorMethod, "{\"error\":\"No such method found!\"}"); // runtime failure

        using var provider = new WayfirePositionProvider(ipcClient);
        var position = await provider.GetAbsolutePositionAsync();

        Assert.Null(position);
        Assert.False(provider.IsSupported);
    }

    [Fact]
    public async Task GetAbsolutePositionAsync_ShouldReturnNull_ForInvalidCursorPayload()
    {
        var ipcClient = new FakeWayfireIpcClient { IsAvailable = true };
        ipcClient.Enqueue(CursorMethod, "{\"pos\":{\"x\":0.0,\"y\":0.0}}"); // capability probe
        ipcClient.Enqueue(OutputsMethod, OutputsWithNegativeOrigin());
        ipcClient.Enqueue(CursorMethod, "{\"pos\":{\"x\":\"invalid\",\"y\":10}}");

        using var provider = new WayfirePositionProvider(ipcClient);
        var position = await provider.GetAbsolutePositionAsync();

        Assert.Null(position);
        Assert.True(provider.IsSupported);
    }

    private static string OutputsWithNegativeOrigin()
    {
        return """
               [
                 {
                   "id": 1,
                   "geometry": { "x": -1920, "y": 0, "width": 1920, "height": 1080 }
                 },
                 {
                   "id": 2,
                   "geometry": { "x": 0, "y": 0, "width": 2560, "height": 1440 }
                 }
               ]
               """;
    }

    private sealed class FakeWayfireIpcClient : IWayfireIpcClient
    {
        private readonly Dictionary<string, Queue<string?>> _responses = new(StringComparer.Ordinal);

        public bool IsAvailable { get; set; } = true;
        public string? SocketPath { get; set; } = "/tmp/fake-wayfire.socket";

        public void Enqueue(string method, string? response)
        {
            if (!_responses.TryGetValue(method, out var queue))
            {
                queue = new Queue<string?>();
                _responses[method] = queue;
            }

            queue.Enqueue(response);
        }

        public Task<string?> SendRequestAsync(string method, CancellationToken cancellationToken = default)
        {
            if (_responses.TryGetValue(method, out var queue) && queue.Count > 0)
            {
                return Task.FromResult(queue.Dequeue());
            }

            return Task.FromResult<string?>(null);
        }

        public void Dispose()
        {
        }
    }
}
