using CrossMacro.Platform.Linux.DisplayServer.Wayland;
using CrossMacro.Platform.Linux.Services.Keyboard;

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
            name => name == "Turkish" ? "tr" : null);

        Assert.Equal("tr", layout);
    }

    [Fact]
    public void TryParseLayout_ReturnsActiveLayout_FromCliJsonResponse()
    {
        var layout = NiriLayoutSource.TryParseLayout(
            """
            { "names": ["English (US)", "German"], "current_idx": 0 }
            """,
            name => name == "English (US)" ? "us" : null);

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
    public void DetectLayout_ReturnsCurrentLayout_FromIpcClient()
    {
        using var source = new DisposableNiriLayoutSource(
            new FakeNiriIpcClient(
                """
                { "Ok": { "KeyboardLayouts": { "names": ["English (US)", "Turkish"], "current_idx": 1 } } }
                """),
            name => name == "Turkish" ? "tr" : null);

        var layout = source.DetectLayout();

        Assert.Equal("tr", layout);
    }

    [Fact]
    public void DetectLayout_ReturnsNull_WhenIpcUnavailable()
    {
        using var source = new DisposableNiriLayoutSource(
            new FakeNiriIpcClient(null, isAvailable: false),
            name => name == "Turkish" ? "tr" : null);

        var layout = source.DetectLayout();

        Assert.Null(layout);
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

        public string? DetectLayout() => _source.DetectLayout();

        public void Dispose()
        {
            Assert.True(_client.Disposed);
        }
    }

    private sealed class FakeNiriIpcClient : INiriIpcClient
    {
        private readonly string? _response;

        public FakeNiriIpcClient(string? response, bool isAvailable = true)
        {
            _response = response;
            IsAvailable = isAvailable;
        }

        public bool IsAvailable { get; }

        public bool Disposed { get; private set; }

        public string? SocketPath => IsAvailable ? "/run/user/1000/niri.sock" : null;

        public Task<string?> SendRequestAsync(string requestJson, CancellationToken cancellationToken = default)
        {
            Assert.Equal("\"KeyboardLayouts\"", requestJson);
            return Task.FromResult(_response);
        }

        public void Dispose()
        {
            Disposed = true;
        }
    }
}
