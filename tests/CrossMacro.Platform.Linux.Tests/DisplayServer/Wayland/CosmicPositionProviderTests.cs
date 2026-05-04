namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;

using CrossMacro.Platform.Linux.DisplayServer.Wayland;

public sealed class CosmicPositionProviderTests
{
    [Fact]
    public void TryParseScreenResolution_ShouldReturnUnionOfEnabledOutputs()
    {
        var parsed = CosmicPositionProvider.TryParseScreenResolution(TwoMonitorKdl(), out var width, out var height);

        Assert.True(parsed);
        Assert.Equal(5120, width);
        Assert.Equal(1440, height);
    }

    [Fact]
    public void TryParseScreenResolution_ShouldIgnoreDisabledAndMirroredOutputs()
    {
        var kdl = """
                  output "DP-1" enabled=#true {
                    position 0 0
                    scale 1.00
                    transform "normal"
                    modes {
                      mode 1920 1080 60000 current=#true preferred=#true
                    }
                  }
                  output "DP-2" enabled=#false {
                    position 1920 0
                    scale 1.00
                    transform "normal"
                    modes {
                      mode 9999 9999 60000 current=#true preferred=#true
                    }
                  }
                  output "DP-3" enabled=#true {
                    mirroring "DP-1"
                    position 1920 0
                    scale 1.00
                    transform "normal"
                    modes {
                      mode 9999 9999 60000 current=#true preferred=#true
                    }
                  }
                  """;

        var parsed = CosmicPositionProvider.TryParseScreenResolution(kdl, out var width, out var height);

        Assert.True(parsed);
        Assert.Equal(1920, width);
        Assert.Equal(1080, height);
    }

    [Fact]
    public void TryParseScreenResolution_ShouldApplyScaleAndQuarterTurnTransform()
    {
        var kdl = """
                  output "DP-1" enabled=#true {
                    position -720 0
                    scale 2.00
                    transform "rotate90"
                    modes {
                      mode 1440 2560 60000 current=#true preferred=#true
                    }
                  }
                  output "DP-2" enabled=#true {
                    position 0 0
                    scale 1.25
                    transform "normal"
                    modes {
                      mode 2560 1440 60000 current=#true preferred=#true
                    }
                  }
                  """;

        var parsed = CosmicPositionProvider.TryParseScreenResolution(kdl, out var width, out var height);

        Assert.True(parsed);
        Assert.Equal(2768, width);
        Assert.Equal(1152, height);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not kdl")]
    [InlineData("output \"DP-1\" enabled=#true { position 999999999999999999999999 0 scale 1.00 modes { mode 1920 1080 60000 current=#true } }")]
    [InlineData("output \"DP-1\" enabled=#false { position 0 0 scale 1.00 modes { mode 1920 1080 60000 current=#true } }")]
    [InlineData("output \"DP-1\" enabled=#true { position 0 0 scale 1.00 modes { mode 1920 1080 60000 } }")]
    public void TryParseScreenResolution_ShouldReturnFalse_ForUnavailableResolution(string? kdl)
    {
        var parsed = CosmicPositionProvider.TryParseScreenResolution(kdl, out var width, out var height);

        Assert.False(parsed);
        Assert.Equal(0, width);
        Assert.Equal(0, height);
    }

    [Fact]
    public async Task GetScreenResolutionAsync_ShouldReturnResolution_WhenCommandOutputIsValid()
    {
        using var provider = new CosmicPositionProvider(_ => Task.FromResult<string?>(TwoMonitorKdl()));

        var resolution = await provider.GetScreenResolutionAsync();

        Assert.False(provider.IsSupported);
        Assert.Null(await provider.GetAbsolutePositionAsync());
        Assert.Equal((5120, 1440), resolution);
    }

    [Fact]
    public async Task GetScreenResolutionAsync_ShouldReturnNull_WhenCommandOutputUnavailable()
    {
        using var provider = new CosmicPositionProvider(_ => Task.FromResult<string?>(null));

        var resolution = await provider.GetScreenResolutionAsync();

        Assert.Null(resolution);
    }

    private static string TwoMonitorKdl()
    {
        return """
               output "DP-2" enabled=#true {
                 description make="LG Electronics" model="LG ULTRAGEAR"
                 physical 600 340
                 position 0 0
                 scale 1.00
                 transform "normal"
                 xwayland_primary #true
                 modes {
                   mode 2560 1440 143973 current=#true preferred=#true
                   mode 1920 1080 60000
                 }
               }
               output "DP-1" enabled=#true {
                 description make="LG Electronics" model="LG ULTRAGEAR"
                 physical 600 340
                 position 2560 0
                 scale 1.00
                 transform "normal"
                 xwayland_primary #false
                 modes {
                   mode 2560 1440 143973 current=#true preferred=#true
                   mode 1920 1080 60000
                 }
               }
               """;
    }
}
