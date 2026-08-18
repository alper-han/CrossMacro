namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;


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
    public void TryParseDesktopBounds_ShouldPreserveNegativeLogicalOrigin()
    {
        var parsed = NiriPositionProvider.TryParseDesktopBounds(
            OutputsResponseWithNegativeOrigin(),
            out var bounds);

        Assert.True(parsed);
        Assert.Equal(new ScreenRect(-1920, 0, 4480, 1440), bounds);
    }

    [Fact]
    public void TryParseScreenResolution_ShouldSupportWrappedOutputsResponse()
    {
        var response = $@"{{ ""Ok"": {{ ""Outputs"": {OutputsObjectWithSingleMonitor()} }} }}" + '\n';

        var parsed = NiriPositionProvider.TryParseScreenResolution(response, out var width, out var height);

        Assert.True(parsed);
        Assert.Equal(2560, width);
        Assert.Equal(1440, height);
    }

    [Fact]
    public void TryParseScreenResolution_ShouldIgnoreDisabledOutputs()
    {
        string response = "{\n"
                       + "  \"Outputs\": {\n"
                       + "    \"DP-1\": {\n"
                       + "      \"current_mode\": null,\n"
                       + "      \"logical\": { \"x\": 0, \"y\": 0, \"width\": 9999, \"height\": 9999 }\n"
                       + "    },\n"
                       + "    \"HDMI-A-1\": {\n"
                       + "      \"current_mode\": 0,\n"
                       + "      \"logical\": { \"x\": 0, \"y\": 0, \"width\": 1920, \"height\": 1080 }\n"
                       + "    }\n"
                       + "  }\n"
                       + "}" + '\n';

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
    public void TryParseDesktopBounds_ShouldRejectOverflowingOutputExtent()
    {
        const string response = """
            {
              "Outputs": {
                "DP-1": {
                  "current_mode": 0,
                  "logical": { "x": 2147483640, "y": 0, "width": 100, "height": 100 }
                }
              }
            }
            """;

        Assert.False(NiriPositionProvider.TryParseDesktopBounds(response, out _));
    }

    [Fact]
    public async Task GetScreenResolutionAsync_ShouldReturnResolution_WhenIpcResponseIsValid()
    {
        using var provider = new NiriPositionProvider(new FakeNiriIpcClient(OutputsResponseWithNegativeOrigin()));

        var resolution = await provider.GetScreenResolutionAsync();
        var bounds = await provider.GetDesktopBoundsAsync();

        Assert.False(provider.IsSupported);
        Assert.Null(await provider.GetAbsolutePositionAsync());
        Assert.Equal((4480, 1440), resolution);
        Assert.Equal(new ScreenRect(-1920, 0, 4480, 1440), bounds);
    }

    [Fact]
    public async Task GetScreenResolutionAsync_ShouldReturnNull_WhenIpcUnavailable()
    {
        using var provider = new NiriPositionProvider(new FakeNiriIpcClient(response: null, isAvailable: false));

        var resolution = await provider.GetScreenResolutionAsync();

        Assert.Null(resolution);
    }

    private static string OutputsResponseWithNegativeOrigin()
    {
        return "{\n"
             + "  \"Outputs\": {\n"
             + "    \"DP-1\": {\n"
             + "      \"name\": \"DP-1\",\n"
             + "      \"modes\": [{ \"width\": 1920, \"height\": 1080, \"refresh_rate\": 60000 }],\n"
             + "      \"current_mode\": 0,\n"
             + "      \"logical\": { \"x\": -1920, \"y\": 0, \"width\": 1920, \"height\": 1080, \"scale\": 1.0 }\n"
             + "    },\n"
             + "    \"HDMI-A-1\": {\n"
             + "      \"name\": \"HDMI-A-1\",\n"
             + "      \"modes\": [{ \"width\": 2560, \"height\": 1440, \"refresh_rate\": 60000 }],\n"
             + "      \"current_mode\": 0,\n"
             + "      \"logical\": { \"x\": 0, \"y\": 0, \"width\": 2560, \"height\": 1440, \"scale\": 1.0 }\n"
             + "    }\n"
             + "  }\n"
             + "}" + '\n';
    }

    private static string OutputsObjectWithSingleMonitor()
    {
        return "{\n"
             + "  \"DP-1\": {\n"
             + "    \"name\": \"DP-1\",\n"
             + "    \"modes\": [{ \"width\": 2560, \"height\": 1440, \"refresh_rate\": 60000 }],\n"
             + "    \"current_mode\": 0,\n"
             + "    \"logical\": { \"x\": 0, \"y\": 0, \"width\": 2560, \"height\": 1440, \"scale\": 1.0 }\n"
             + "  }\n"
             + "}" + '\n';
    }

    private sealed class FakeNiriIpcClient(string? response, bool isAvailable = true) : INiriIpcClient
    {
        private readonly string? _response = response;

        public bool IsAvailable { get; } = isAvailable;

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
