
namespace CrossMacro.Infrastructure.Tests.Services.Updates;

public sealed class GitHubUpdateServiceTests : IDisposable
{
    private static GitHubUpdateService CreateService(
        IRuntimeContext runtimeContext,
        HttpMessageHandler handler,
        TimeSpan? timeout = null) =>
        new(
            runtimeContext,
            new HttpClient(handler) { Timeout = timeout ?? TimeSpan.FromSeconds(8) },
            new Version(1, 0, 0));

    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        public required Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> OnSendAsync { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return OnSendAsync(request, cancellationToken);
        }
    }

    private readonly GitHubUpdateService _service;
    private readonly MockHttpMessageHandler _handler;

    public GitHubUpdateServiceTests()
    {
        _handler = new MockHttpMessageHandler
        {
            OnSendAsync = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)),
        };
        _service = CreateService(FakeRuntimeContext.Linux(), _handler);
    }

    public void Dispose() => _handler.Dispose();

    [Fact]
    public async Task CheckForUpdatesAsync_WhenRunningInFlatpak_ShouldSkipHttpCall()
    {
        var service = CreateService(FakeRuntimeContext.Linux(isFlatpak: true), _handler);
        var httpCalled = false;
        _handler.OnSendAsync = (_, _) =>
        {
            httpCalled = true;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        };

        var result = await service.CheckForUpdatesAsync(TestContext.Current.CancellationToken);

        _ = result.HasUpdate.Should().BeFalse();
        _ = httpCalled.Should().BeFalse();
    }

    [Fact]
    public async Task CheckForUpdatesAsync_ShouldReturnUpdate_WhenRemoteIsNewer()
    {
        const string json = "{\"tag_name\": \"v99.99.99\", \"html_url\": \"http://example.com\"}";
        _handler.OnSendAsync = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json),
        });

        var result = await _service.CheckForUpdatesAsync(TestContext.Current.CancellationToken);

        _ = result.HasUpdate.Should().BeTrue();
        _ = result.LatestVersion.Should().Be("99.99.99");
        _ = result.ReleaseUrl.Should().Be(new Uri("http://example.com"));
    }

    [Fact]
    public async Task CheckForUpdatesAsync_ShouldReturnNoUpdate_WhenRemoteIsOld()
    {
        const string json = "{\"tag_name\": \"v0.0.0\", \"html_url\": \"http://example.com\"}";
        _handler.OnSendAsync = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json),
        });

        var result = await _service.CheckForUpdatesAsync(TestContext.Current.CancellationToken);

        _ = result.HasUpdate.Should().BeFalse();
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhenResponseJsonIsMalformed_ShouldReturnNoUpdate()
    {
        _handler.OnSendAsync = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{not-valid-json"),
        });

        var result = await _service.CheckForUpdatesAsync(TestContext.Current.CancellationToken);

        _ = result.HasUpdate.Should().BeFalse();
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhenRequestTimesOut_ShouldReturnNoUpdate()
    {
        using var client = new HttpClient(_handler) { Timeout = TimeSpan.FromMilliseconds(50) };
        var service = new GitHubUpdateService(FakeRuntimeContext.Linux(), client, new Version(1, 0, 0));
        _handler.OnSendAsync = async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5), TimeProvider.System, ct);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"tag_name\": \"v99.99.99\", \"html_url\": \"http://example.com\"}"),
            };
        };

        var result = await service.CheckForUpdatesAsync(TestContext.Current.CancellationToken);

        _ = result.HasUpdate.Should().BeFalse();
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhenRequestThrowsHttpException_ShouldReturnNoUpdate()
    {
        _handler.OnSendAsync = (_, _) => Task.FromException<HttpResponseMessage>(new HttpRequestException("network down"));

        var result = await _service.CheckForUpdatesAsync(TestContext.Current.CancellationToken);

        _ = result.HasUpdate.Should().BeFalse();
    }

    [Theory]
    [InlineData("v99.0.0-beta.1")]
    [InlineData("99.0.0+build.5")]
    public async Task CheckForUpdatesAsync_WhenRemoteTagIsPreRelease_ShouldReturnNoUpdate(string tag)
    {
        _handler.OnSendAsync = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent($"{{\"tag_name\": \"{tag}\", \"html_url\": \"http://example.com\"}}"),
        });

        var result = await _service.CheckForUpdatesAsync(TestContext.Current.CancellationToken);

        _ = result.HasUpdate.Should().BeFalse();
    }

    [Fact]
    public async Task CheckForUpdatesAsync_SendsUserAgentPerRequestWithoutChangingTheClientDefaults()
    {
        using var client = new HttpClient(_handler);
        var service = new GitHubUpdateService(FakeRuntimeContext.Linux(), client, new Version(1, 0, 0));
        HttpRequestMessage? sent = null;
        _handler.OnSendAsync = (request, _) =>
        {
            sent = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        };

        _ = await service.CheckForUpdatesAsync(TestContext.Current.CancellationToken);

        _ = sent.Should().NotBeNull();
        _ = sent.Headers.UserAgent.ToString().Should().Be("CrossMacro-App");
        _ = client.DefaultRequestHeaders.UserAgent.Should().BeEmpty();
    }
}
