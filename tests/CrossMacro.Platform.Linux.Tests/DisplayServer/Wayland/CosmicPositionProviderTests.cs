namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;


public sealed class CosmicPositionProviderTests
{
    [Fact]
    public void TryParseDesktopBounds_ShouldReturnUnionOfEnabledOutputs()
    {
        var parsed = CosmicPositionProvider.TryParseDesktopBounds(TwoMonitorKdl(), out var bounds);

        Assert.True(parsed);
        Assert.Equal(5120, bounds.Width);
        Assert.Equal(1440, bounds.Height);
    }

    [Fact]
    public void TryParseOutputBounds_ShouldReturnEachLogicalOutput()
    {
        var parsed = CosmicPositionProvider.TryParseOutputBounds(TwoMonitorKdl(), out var outputs);

        Assert.True(parsed);
        Assert.Equal(
        [
            new ScreenRect(0, 0, 2560, 1440),
            new ScreenRect(2560, 0, 2560, 1440),
        ], outputs);
    }

    [Fact]
    public void TryParseDesktopBounds_ShouldPreserveNegativeLogicalOrigin()
    {
        const string kdl = """
            output "DP-1" enabled=#true {
              position -1920 -200
              scale 1.00
              transform "normal"
              modes {
                mode 1920 1080 60000 current=#true
              }
            }
            output "DP-2" enabled=#true {
              position 0 0
              scale 1.00
              transform "normal"
              modes {
                mode 2560 1440 60000 current=#true
              }
            }
            """;

        var parsed = CosmicPositionProvider.TryParseDesktopBounds(kdl, out var bounds);

        Assert.True(parsed);
        Assert.Equal(new ScreenRect(-1920, -200, 4480, 1640), bounds);
    }

    [Fact]
    public void TryParseDesktopBounds_ShouldFailClosedWhenVirtualExtentExceedsInt32()
    {
        const string kdl = """
            output "DP-1" enabled=#true {
              position -2147483648 0
              scale 1.00
              transform "normal"
              modes {
                mode 1 1 60000 current=#true
              }
            }
            output "DP-2" enabled=#true {
              position 2147483646 0
              scale 1.00
              transform "normal"
              modes {
                mode 1 1 60000 current=#true
              }
            }
            """;

        Assert.False(CosmicPositionProvider.TryParseDesktopBounds(kdl, out _));
    }

    [Fact]
    public void TryParseDesktopBounds_ShouldIgnoreDisabledAndMirroredOutputs()
    {
        string kdl = "output \"DP-1\" enabled=#true {\n"
                   + "  position 0 0\n"
                   + "  scale 1.00\n"
                   + "  transform \"normal\"\n"
                   + "  modes {\n"
                   + "    mode 1920 1080 60000 current=#true preferred=#true\n"
                   + "  }\n"
                   + "}\n"
                   + "output \"DP-2\" enabled=#false {\n"
                   + "  position 1920 0\n"
                   + "  scale 1.00\n"
                   + "  transform \"normal\"\n"
                   + "  modes {\n"
                   + "    mode 9999 9999 60000 current=#true preferred=#true\n"
                   + "  }\n"
                   + "}\n"
                   + "output \"DP-3\" enabled=#true {\n"
                   + "  mirroring \"DP-1\"\n"
                   + "  position 1920 0\n"
                   + "  scale 1.00\n"
                   + "  transform \"normal\"\n"
                   + "  modes {\n"
                   + "    mode 9999 9999 60000 current=#true preferred=#true\n"
                   + "  }\n"
                   + "}" + '\n';

        var parsed = CosmicPositionProvider.TryParseDesktopBounds(kdl, out var bounds);

        Assert.True(parsed);
        Assert.Equal(1920, bounds.Width);
        Assert.Equal(1080, bounds.Height);
    }

    [Fact]
    public void TryParseDesktopBounds_ShouldApplyScaleAndQuarterTurnTransform()
    {
        string kdl = "output \"DP-1\" enabled=#true {\n"
                   + "  position -720 0\n"
                   + "  scale 2.00\n"
                   + "  transform \"rotate90\"\n"
                   + "  modes {\n"
                   + "    mode 1440 2560 60000 current=#true preferred=#true\n"
                   + "  }\n"
                   + "}\n"
                   + "output \"DP-2\" enabled=#true {\n"
                   + "  position 0 0\n"
                   + "  scale 1.25\n"
                   + "  transform \"normal\"\n"
                   + "  modes {\n"
                   + "    mode 2560 1440 60000 current=#true preferred=#true\n"
                   + "  }\n"
                   + "}" + '\n';

        var parsed = CosmicPositionProvider.TryParseDesktopBounds(kdl, out var bounds);

        Assert.True(parsed);
        Assert.Equal(2768, bounds.Width);
        Assert.Equal(1152, bounds.Height);
    }

    [Fact]
    public void TryParseDesktopBounds_ShouldRejectLongNearMatchModeLine_WhileAcceptingValidMode()
    {
        var filler = new string('x', 100_000);
        var valid = string.Concat(
            "output \"DP-1\" enabled=#true {\n",
            "position 0 0\n",
            "scale 1.00\n",
            "modes {\n",
            "mode 1920 1080 60000 ",
            filler,
            " current=#true\n",
            "}\n",
            "}");
        var nearMatch = valid.Replace("current=#true", "current=#trueX", StringComparison.Ordinal);

        var validParsed = CosmicPositionProvider.TryParseDesktopBounds(valid, out var validBounds);
        var nearMatchParsed = CosmicPositionProvider.TryParseDesktopBounds(nearMatch, out var nearMatchBounds);

        Assert.True(validParsed);
        Assert.Equal(1920, validBounds.Width);
        Assert.Equal(1080, validBounds.Height);
        Assert.False(nearMatchParsed);
        Assert.Equal(0, nearMatchBounds.Width);
        Assert.Equal(0, nearMatchBounds.Height);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not kdl")]
    [InlineData("output \"DP-1\" enabled=#true { position 999999999999999999999999 0 scale 1.00 modes { mode 1920 1080 60000 current=#true } }")]
    [InlineData("output \"DP-1\" enabled=#false { position 0 0 scale 1.00 modes { mode 1920 1080 60000 current=#true } }")]
    [InlineData("output \"DP-1\" enabled=#true { position 0 0 scale 1.00 modes { mode 1920 1080 60000 } }")]
    public void TryParseDesktopBounds_ShouldReturnFalse_ForUnavailableResolution(string? kdl)
    {
        var parsed = CosmicPositionProvider.TryParseDesktopBounds(kdl, out var bounds);

        Assert.False(parsed);
        Assert.Equal(0, bounds.Width);
        Assert.Equal(0, bounds.Height);
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CreateCosmicRandrStartInfo_ShouldUseHostBridgeOnlyForFlatpak(
        bool useFlatpakHostCommand)
    {
        var startInfo = CosmicPositionProvider.CreateCosmicRandrStartInfo(useFlatpakHostCommand);
        string[] expectedArguments = useFlatpakHostCommand
            ? ["--host", "--watch-bus", "cosmic-randr", "list", "--kdl"]
            : ["list", "--kdl"];

        Assert.Equal(useFlatpakHostCommand ? "flatpak-spawn" : "cosmic-randr", startInfo.FileName);
        Assert.Equal(expectedArguments, startInfo.ArgumentList, StringComparer.Ordinal);
        Assert.False(startInfo.UseShellExecute);
        Assert.True(startInfo.RedirectStandardOutput);
        Assert.True(startInfo.RedirectStandardError);
    }

    private static string TwoMonitorKdl()
    {
        return "output \"DP-2\" enabled=#true {\n"
             + "  description make=\"LG Electronics\" model=\"LG ULTRAGEAR\"\n"
             + "  physical 600 340\n"
             + "  position 0 0\n"
             + "  scale 1.00\n"
             + "  transform \"normal\"\n"
             + "  xwayland_primary #true\n"
             + "  modes {\n"
             + "    mode 2560 1440 143973 current=#true preferred=#true\n"
             + "    mode 1920 1080 60000\n"
             + "  }\n"
             + "}\n"
             + "output \"DP-1\" enabled=#true {\n"
             + "  description make=\"LG Electronics\" model=\"LG ULTRAGEAR\"\n"
             + "  physical 600 340\n"
             + "  position 2560 0\n"
             + "  scale 1.00\n"
             + "  transform \"normal\"\n"
             + "  xwayland_primary #false\n"
             + "  modes {\n"
             + "    mode 2560 1440 143973 current=#true preferred=#true\n"
             + "    mode 1920 1080 60000\n"
             + "  }\n"
             + "}" + '\n';
    }
}
