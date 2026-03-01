using System;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Services;
using FluentAssertions;
using Xunit;

namespace CrossMacro.Core.Tests.Services;

public class GitHubUpdateServiceTests
{
    private class TestableGitHubUpdateService : GitHubUpdateService
    {
        public required HttpMessageHandler Handler { get; set; }

        public TestableGitHubUpdateService(IRuntimeContext runtimeContext)
            : base(runtimeContext)
        {
        }

        protected override HttpClient CreateClient()
        {
            return new HttpClient(Handler);
        }
    }

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        public required Func<HttpRequestMessage, HttpResponseMessage> OnSend { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(OnSend(request));
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
        _handler = new MockHttpMessageHandler { OnSend = _ => new HttpResponseMessage(HttpStatusCode.NotFound) };
        _service = new TestableGitHubUpdateService(_runtimeContext) { Handler = _handler };
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhenRunningInFlatpak_ShouldSkipHttpCall()
    {
        _runtimeContext.IsFlatpak = true;
        var httpCalled = false;
        _handler.OnSend = _ =>
        {
            httpCalled = true;
            return new HttpResponseMessage(HttpStatusCode.OK);
        };

        var result = await _service.CheckForUpdatesAsync();

        result.HasUpdate.Should().BeFalse();
        httpCalled.Should().BeFalse();
    }

    [Fact]
    public async Task CheckForUpdatesAsync_ShouldReturnUpdate_WhenRemoteIsNewer()
    {
        _runtimeContext.IsFlatpak = false;

        // Arrange
        // Assume current version is less than 9.9.9
        var json = "{\"tag_name\": \"v99.99.99\", \"html_url\": \"http://example.com\"}";
        _handler.OnSend = req => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json)
        };

        // Act
        var result = await _service.CheckForUpdatesAsync();

        // Assert
        result.HasUpdate.Should().BeTrue();
        result.LatestVersion.Should().Be("99.99.99");
        result.ReleaseUrl.Should().Be("http://example.com");
    }

    [Fact]
    public async Task CheckForUpdatesAsync_ShouldReturnNoUpdate_WhenRemoteIsOld()
    {
        _runtimeContext.IsFlatpak = false;

        // Arrange
        var json = "{\"tag_name\": \"v0.0.0\", \"html_url\": \"http://example.com\"}";
        _handler.OnSend = req => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json)
        };

        // Act
        var result = await _service.CheckForUpdatesAsync();

        // Assert
        result.HasUpdate.Should().BeFalse();
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhenNotFlatpak_ShouldInvokeHttpClient()
    {
        _runtimeContext.IsFlatpak = false;
        var httpCalled = false;
        _handler.OnSend = _ =>
        {
            httpCalled = true;
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        };

        _ = await _service.CheckForUpdatesAsync();

        httpCalled.Should().BeTrue();
    }
}
