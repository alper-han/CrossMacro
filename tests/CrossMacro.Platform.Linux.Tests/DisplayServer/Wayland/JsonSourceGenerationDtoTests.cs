
namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;

public sealed class JsonSourceGenerationDtoTests
{
    [Fact]
    public void Contexts_ShouldKeepRootsAndOptions()
    {
        AssertRoots(
            SwayJsonContext.Default,
            typeof(SwayNodeDto),
            typeof(SwayWorkspaceDto[]),
            typeof(SwayOutputDto[]),
            typeof(SwayCommandResultDto[]));
        Assert.False(SwayJsonContext.Default.Options.WriteIndented);
        Assert.Null(SwayJsonContext.Default.Options.PropertyNamingPolicy);

        AssertRoots(
            HyprlandJsonContext.Default,
            typeof(HyprlandWindowDto),
            typeof(HyprlandWindowDto[]),
            typeof(HyprlandActiveWorkspaceDto));
        Assert.False(HyprlandJsonContext.Default.Options.WriteIndented);
        Assert.Null(HyprlandJsonContext.Default.Options.PropertyNamingPolicy);

        AssertRoots(
            NiriJsonContext.Default,
            typeof(NiriResponse<NiriFocusedWindowData>),
            typeof(NiriResponse<NiriWindowsData>),
            typeof(NiriResponse<NiriWorkspacesData>),
            typeof(NiriResponse<NiriOutputsData>));
        Assert.True(NiriJsonContext.Default.Options.WriteIndented);
        Assert.Equal("sampleName", NiriJsonContext.Default.Options.PropertyNamingPolicy!.ConvertName("SampleName"));
    }

    [Fact]
    public void Contexts_ShouldKeepWirePropertyNames()
    {
        Assert.Equal("app_id", PropertyName(SwayJsonContext.Default.GetTypeInfo(typeof(SwayNodeDto)), nameof(SwayNodeDto.AppId)));
        Assert.Equal("floating_nodes", PropertyName(SwayJsonContext.Default.GetTypeInfo(typeof(SwayNodeDto)), nameof(SwayNodeDto.FloatingNodes)));
        Assert.Equal("focusHistoryID", PropertyName(HyprlandJsonContext.Default.GetTypeInfo(typeof(HyprlandWindowDto)), nameof(HyprlandWindowDto.FocusHistoryId)));
        Assert.Equal("Ok", PropertyName(NiriJsonContext.Default.GetTypeInfo(typeof(NiriResponse<NiriWindowsData>)), nameof(NiriResponse<NiriWindowsData>.Ok)));
        Assert.Equal("FocusedWindow", PropertyName(NiriJsonContext.Default.GetTypeInfo(typeof(NiriFocusedWindowData)), nameof(NiriFocusedWindowData.FocusedWindow)));
        Assert.Equal("app_id", PropertyName(NiriJsonContext.Default.GetTypeInfo(typeof(NiriWindowDto)), nameof(NiriWindowDto.AppId)));
    }

    [Fact]
    public void RepresentativeFixtures_ShouldDeserializeThroughGeneratedContexts()
    {
        var sway = JsonSerializer.Deserialize(
            "{\"id\":7,\"app_id\":\"org.example\",\"window_properties\":{\"class\":\"Example\"},\"rect\":{\"x\":10,\"y\":20,\"width\":800,\"height\":600},\"floating_nodes\":[]}",
            SwayJsonContext.Default.SwayNodeDto);
        Assert.NotNull(sway);
        Assert.Equal("org.example", sway.AppId);
        Assert.Equal("Example", sway.WindowProperties!.Class);
        Assert.Equal(800, sway.Rect!.Width);

        var hyprland = JsonSerializer.Deserialize(
            "{\"address\":\"0x123\",\"title\":\"Example\",\"class\":\"org.example\",\"pid\":42,\"focusHistoryID\":0,\"at\":[10,20],\"size\":[800,600],\"workspace\":{\"id\":1,\"name\":\"1\"}}",
            HyprlandJsonContext.Default.HyprlandWindowDto);
        Assert.NotNull(hyprland);
        Assert.Equal(0, hyprland.FocusHistoryId);
        Assert.Equal("1", hyprland.Workspace!.Name);
        Assert.Equal(800, hyprland.Size![0]);

        var niri = JsonSerializer.Deserialize(
            "{\"Ok\":{\"FocusedWindow\":{\"id\":9,\"app_id\":\"org.example\",\"layout\":{\"window_size\":[800,600]}}}}",
            NiriJsonContext.Default.NiriResponseNiriFocusedWindowData);
        Assert.NotNull(niri);
        Assert.Equal(9UL, niri.Ok!.FocusedWindow!.Id);
        Assert.Equal("org.example", niri.Ok.FocusedWindow.AppId);
        Assert.Equal(800, niri.Ok.FocusedWindow.Layout!.WindowSize![0]);
    }

    private static void AssertRoots(JsonSerializerContext context, params Type[] rootTypes)
    {
        foreach (var rootType in rootTypes)
        {
            Assert.NotNull(context.GetTypeInfo(rootType));
        }
    }

    private static string PropertyName(JsonTypeInfo? typeInfo, string propertyName)
    {
        Assert.NotNull(typeInfo);
        return typeInfo.Properties
            .Single(property => property.AttributeProvider is System.Reflection.MemberInfo member
                && string.Equals(member.Name, propertyName, StringComparison.Ordinal))
            .Name;
    }
}
