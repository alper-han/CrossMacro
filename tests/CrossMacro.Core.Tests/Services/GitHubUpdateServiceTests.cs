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

    private readonly TestableGitHubUpdateService _service;
    private readonly MockHttpMessageHandler _handler;

    public GitHubUpdateServiceTests()
    {
        _handler = new MockHttpMessageHandler { OnSend = _ => new HttpResponseMessage(HttpStatusCode.NotFound) };
        _service = new TestableGitHubUpdateService { Handler = _handler };
    }

    [Fact]
    public async Task CheckForUpdatesAsync_ShouldReturnUpdate_WhenRemoteIsNewer()
    {
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
}
