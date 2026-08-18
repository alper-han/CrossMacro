
namespace CrossMacro.Platform.Linux.Tests.Services.Keyboard;

public sealed class NiriLayoutSourceTests
{
    [Fact]
    public void TryParseLayout_ReturnsActiveLayout_FromWrappedIpcResponse()
    {
        var layout = NiriLayoutSource.TryParseLayout(
            """
            { "Ok": { "KeyboardLayouts": { "names": ["English (US)", "Turkish"], "current_idx": 1 } } }
            """,
            name => name is "Turkish" ? "tr" : null);

        Assert.Equal("tr", layout);
    }

    [Fact]
    public void TryParseLayout_ReturnsActiveLayout_FromCliJsonResponse()
    {
        var layout = NiriLayoutSource.TryParseLayout(
            """
            { "names": ["English (US)", "German"], "current_idx": 0 }
            """,
            name => name is "English (US)" ? "us" : null);

        Assert.Equal("us", layout);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{ \"Ok\": { \"KeyboardLayouts\": { \"names\": [], \"current_idx\": 0 } } }")]
    [InlineData("{ \"Ok\": { \"KeyboardLayouts\": { \"names\": [\"English (US)\"], \"current_idx\": 1 } } }")]
    public void TryParseLayout_ReturnsNull_ForInvalidResponse(string? response)
    {
        var layout = NiriLayoutSource.TryParseLayout(response, _ => null);

        Assert.Null(layout);
    }

    [Fact]
    public async Task DetectLayoutAsync_ReturnsCurrentLayout_FromIpcClient()
    {
        using var source = new DisposableNiriLayoutSource(
            new FakeNiriIpcClient(
                """
                { "Ok": { "KeyboardLayouts": { "names": ["English (US)", "Turkish"], "current_idx": 1 } } }
                """),
            name => name is "Turkish" ? "tr" : null);

        var layout = await source.DetectLayoutAsync();

        Assert.Equal("tr", layout);
    }

    [Fact]
    public async Task DetectLayoutAsync_ReturnsNull_WhenIpcUnavailable()
    {
        using var source = new DisposableNiriLayoutSource(
            new FakeNiriIpcClient(response: null, isAvailable: false),
            name => name is "Turkish" ? "tr" : null);

        var layout = await source.DetectLayoutAsync();

        Assert.Null(layout);
    }

    [Fact]
    public async Task DetectLayoutAsync_ReturnsNull_WhenIpcFails()
    {
        var client = new FakeNiriIpcClient(response: null, isAvailable: true, exception: new IOException("socket closed"));
        var source = new NiriLayoutSource(() => client, _ => "us");

        var layout = await source.DetectLayoutAsync();

        Assert.Null(layout);
        Assert.True(client.Disposed);
    }

    [Fact]
    public async Task DetectLayoutAsync_ThrowsWhenCanceled()
    {
        using var source = new DisposableNiriLayoutSource(
            new FakeNiriIpcClient(response: null, isAvailable: true, canceledResponse: Task.FromCanceled<string?>(new CancellationToken(canceled: true))),
            name => name is "Turkish" ? "tr" : null);

        await TestAssertions.ThrowsAnyAsync<OperationCanceledException>(() => source.DetectLayoutAsync(new CancellationToken(canceled: true)));
    }

    private sealed class DisposableNiriLayoutSource : IDisposable
    {
        private readonly FakeNiriIpcClient _client;
        private readonly NiriLayoutSource _source;

        public DisposableNiriLayoutSource(FakeNiriIpcClient client, Func<string, string?> resolveLayoutName)
        {
            _client = client;
            _source = new NiriLayoutSource(() => _client, resolveLayoutName);
        }

        public Task<string?> DetectLayoutAsync(CancellationToken cancellationToken = default) => _source.DetectLayoutAsync(cancellationToken);

        public void Dispose()
        {
            Assert.True(_client.Disposed);
        }
    }

    private sealed class FakeNiriIpcClient : INiriIpcClient
    {
        private readonly string? _response;
        private readonly Task<string?>? _canceledResponse;
        private readonly Exception? _exception;

        public FakeNiriIpcClient(
            string? response,
            bool isAvailable = true,
            Task<string?>? canceledResponse = null,
            Exception? exception = null)
        {
            _response = response;
            IsAvailable = isAvailable;
            _canceledResponse = canceledResponse;
            _exception = exception;
        }

        public bool IsAvailable { get; }

        public bool Disposed { get; private set; }

        public string? SocketPath => IsAvailable ? "/run/user/1000/niri.sock" : null;

        public Task<string?> SendRequestAsync(string requestJson, CancellationToken cancellationToken = default)
        {
            Assert.Equal("\"KeyboardLayouts\"", requestJson);
            if (_exception is not null)
            {
                return Task.FromException<string?>(_exception);
            }

            return _canceledResponse ?? Task.FromResult(_response);
        }

        public void Dispose()
        {
            Disposed = true;
        }
    }
}
