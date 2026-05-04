namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;

using CrossMacro.Platform.Linux.DisplayServer.Wayland;

public sealed class NiriPositionProviderTests
{
    [Fact]
    public void TryParseScreenResolution_ShouldReturnUnionOfEnabledLogicalOutputs()
    {
        var parsed = NiriPositionProvider.TryParseScreenResolution(OutputsResponseWithNegativeOrigin(), out var width, out var height);

        Assert.True(parsed);
        Assert.Equal(4480, width);
        Assert.Equal(1440, height);
    }

    [Fact]
    public void TryParseScreenResolution_ShouldSupportWrappedOutputsResponse()
    {
        var response = $$"""
                       { "Ok": { "Outputs": {{OutputsObjectWithSingleMonitor()}} } }
                       """;

        var parsed = NiriPositionProvider.TryParseScreenResolution(response, out var width, out var height);

        Assert.True(parsed);
        Assert.Equal(2560, width);
        Assert.Equal(1440, height);
    }

    [Fact]
    public void TryParseScreenResolution_ShouldIgnoreDisabledOutputs()
    {
        var response = """
                       {
                         "Outputs": {
                           "DP-1": {
                             "current_mode": null,
                             "logical": { "x": 0, "y": 0, "width": 9999, "height": 9999 }
                           },
                           "HDMI-A-1": {
                             "current_mode": 0,
                             "logical": { "x": 0, "y": 0, "width": 1920, "height": 1080 }
                           }
                         }
                       }
                       """;

        var parsed = NiriPositionProvider.TryParseScreenResolution(response, out var width, out var height);

        Assert.True(parsed);
        Assert.Equal(1920, width);
        Assert.Equal(1080, height);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{ \"Outputs\": {} }")]
    [InlineData("{ \"Outputs\": { \"DP-1\": { \"current_mode\": null } } }")]
    [InlineData("{ \"Outputs\": { \"DP-1\": { \"current_mode\": 0, \"logical\": { \"x\": 0, \"y\": 0, \"width\": 0, \"height\": 1080 } } } }")]
    public void TryParseScreenResolution_ShouldReturnFalse_ForUnavailableResolution(string? response)
    {
        var parsed = NiriPositionProvider.TryParseScreenResolution(response, out var width, out var height);

        Assert.False(parsed);
        Assert.Equal(0, width);
        Assert.Equal(0, height);
    }

    [Fact]
    public async Task GetScreenResolutionAsync_ShouldReturnResolution_WhenIpcResponseIsValid()
    {
        using var provider = new NiriPositionProvider(new FakeNiriIpcClient(OutputsResponseWithNegativeOrigin()));

        var resolution = await provider.GetScreenResolutionAsync();

        Assert.False(provider.IsSupported);
        Assert.Null(await provider.GetAbsolutePositionAsync());
        Assert.Equal((4480, 1440), resolution);
    }

    [Fact]
    public async Task GetScreenResolutionAsync_ShouldReturnNull_WhenIpcUnavailable()
    {
        using var provider = new NiriPositionProvider(new FakeNiriIpcClient(null, isAvailable: false));

        var resolution = await provider.GetScreenResolutionAsync();

        Assert.Null(resolution);
    }

    private static string OutputsResponseWithNegativeOrigin()
    {
        return """
               {
                 "Outputs": {
                   "DP-1": {
                     "name": "DP-1",
                     "modes": [{ "width": 1920, "height": 1080, "refresh_rate": 60000 }],
                     "current_mode": 0,
                     "logical": { "x": -1920, "y": 0, "width": 1920, "height": 1080, "scale": 1.0 }
                   },
                   "HDMI-A-1": {
                     "name": "HDMI-A-1",
                     "modes": [{ "width": 2560, "height": 1440, "refresh_rate": 60000 }],
                     "current_mode": 0,
                     "logical": { "x": 0, "y": 0, "width": 2560, "height": 1440, "scale": 1.0 }
                   }
                 }
               }
               """;
    }

    private static string OutputsObjectWithSingleMonitor()
    {
        return """
               {
                 "DP-1": {
                   "name": "DP-1",
                   "modes": [{ "width": 2560, "height": 1440, "refresh_rate": 60000 }],
                   "current_mode": 0,
                   "logical": { "x": 0, "y": 0, "width": 2560, "height": 1440, "scale": 1.0 }
                 }
               }
               """;
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

        public string? SocketPath => IsAvailable ? "/run/user/1000/niri.sock" : null;

        public Task<string?> SendRequestAsync(string requestJson, CancellationToken cancellationToken = default)
        {
            Assert.Equal("\"Outputs\"", requestJson);
            return Task.FromResult(_response);
        }

        public void Dispose()
        {
        }
    }
}
