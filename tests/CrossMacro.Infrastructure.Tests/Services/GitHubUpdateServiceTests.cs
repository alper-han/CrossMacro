
namespace CrossMacro.Infrastructure.Tests.Services;

public sealed class GitHubUpdateServiceTests : IDisposable
{
    private sealed class TestableGitHubUpdateService(IRuntimeContext runtimeContext) : GitHubUpdateService(runtimeContext)
    {
        public required HttpMessageHandler Handler { get; set; }
        public Version CurrentVersion { get; set; } = new(1, 0, 0);
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(8);

        protected override HttpClient CreateClient()
        {
            return new HttpClient(Handler);
        }

        protected override Version? GetCurrentVersion()
        {
            return CurrentVersion;
        }

        protected override TimeSpan RequestTimeout => Timeout;
    }

    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        public required Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> OnSendAsync { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return OnSendAsync(request, cancellationToken);
        }
    }

    private sealed class TestRuntimeContext : IRuntimeContext
    {
        public bool IsLinux => true;
        public bool IsWindows => false;
        public bool IsMacOS => false;
        public bool IsFlatpak { get; set; }
        public string? SessionType => "x11";
    }

    private readonly TestableGitHubUpdateService _service;
    private readonly MockHttpMessageHandler _handler;
    private readonly TestRuntimeContext _runtimeContext;

    public GitHubUpdateServiceTests()
    {
        _runtimeContext = new TestRuntimeContext();
        _handler = new MockHttpMessageHandler
        {
            OnSendAsync = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)),
        };
        _service = new TestableGitHubUpdateService(_runtimeContext) { Handler = _handler };
    }

    public void Dispose() => _handler.Dispose();

    [Fact]
    public async Task CheckForUpdatesAsync_WhenRunningInFlatpak_ShouldSkipHttpCall()
    {
        _runtimeContext.IsFlatpak = true;
        var httpCalled = false;
        _handler.OnSendAsync = (_, _) =>
        {
            httpCalled = true;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        };

        var result = await _service.CheckForUpdatesAsync();

        _ = result.HasUpdate.Should().BeFalse();
        _ = httpCalled.Should().BeFalse();
    }

    [Fact]
    public async Task CheckForUpdatesAsync_ShouldReturnUpdate_WhenRemoteIsNewer()
    {
        _runtimeContext.IsFlatpak = false;

        const string json = "{\"tag_name\": \"v99.99.99\", \"html_url\": \"http://example.com\"}";
        _handler.OnSendAsync = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json),
        });

        var result = await _service.CheckForUpdatesAsync();

        _ = result.HasUpdate.Should().BeTrue();
        _ = result.LatestVersion.Should().Be("99.99.99");
        _ = result.ReleaseUrl.Should().Be(new Uri("http://example.com"));
    }

    [Fact]
    public async Task CheckForUpdatesAsync_ShouldReturnNoUpdate_WhenRemoteIsOld()
    {
        _runtimeContext.IsFlatpak = false;

        const string json = "{\"tag_name\": \"v0.0.0\", \"html_url\": \"http://example.com\"}";
        _handler.OnSendAsync = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json),
        });

        var result = await _service.CheckForUpdatesAsync();

        _ = result.HasUpdate.Should().BeFalse();
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhenNotFlatpak_ShouldInvokeHttpClient()
    {
        _runtimeContext.IsFlatpak = false;
        var httpCalled = false;
        _handler.OnSendAsync = (_, _) =>
        {
            httpCalled = true;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        };

        _ = await _service.CheckForUpdatesAsync();

        _ = httpCalled.Should().BeTrue();
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhenResponseJsonIsMalformed_ShouldReturnNoUpdate()
    {
        _runtimeContext.IsFlatpak = false;
        _handler.OnSendAsync = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{not-valid-json"),
        });

        var result = await _service.CheckForUpdatesAsync();

        _ = result.HasUpdate.Should().BeFalse();
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhenRequestTimesOut_ShouldReturnNoUpdate()
    {
        _runtimeContext.IsFlatpak = false;
        _service.Timeout = TimeSpan.FromMilliseconds(50);
        _handler.OnSendAsync = async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"tag_name\": \"v99.99.99\", \"html_url\": \"http://example.com\"}"),
            };
        };

        var result = await _service.CheckForUpdatesAsync();

        _ = result.HasUpdate.Should().BeFalse();
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhenRequestThrowsHttpException_ShouldReturnNoUpdate()
    {
        _runtimeContext.IsFlatpak = false;
        _handler.OnSendAsync = (_, _) => Task.FromException<HttpResponseMessage>(new HttpRequestException("network down"));

        var result = await _service.CheckForUpdatesAsync();

        _ = result.HasUpdate.Should().BeFalse();
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhenDeserializerThrowsJsonException_ShouldReturnNoUpdate()
    {
        _runtimeContext.IsFlatpak = false;
        _handler.OnSendAsync = (_, _) => Task.FromException<HttpResponseMessage>(new JsonException("bad payload"));

        var result = await _service.CheckForUpdatesAsync();

        _ = result.HasUpdate.Should().BeFalse();
    }
}
