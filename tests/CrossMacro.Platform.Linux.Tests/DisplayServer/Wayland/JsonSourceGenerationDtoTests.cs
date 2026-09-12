
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

        AssertRoots(
            GnomeJsonContext.Default,
            typeof(WindowInfo),
            typeof(WindowInfo[]));
        Assert.False(GnomeJsonContext.Default.Options.WriteIndented);
        Assert.Null(GnomeJsonContext.Default.Options.PropertyNamingPolicy);
    }

    [Fact]
    public void Contexts_ShouldKeepWirePropertyNames()
    {
        Assert.Equal("app_id", PropertyName(SwayJsonContext.Default.GetTypeInfo(typeof(SwayNodeDto)), nameof(SwayNodeDto.AppId)));
        Assert.Equal("floating_nodes", PropertyName(SwayJsonContext.Default.GetTypeInfo(typeof(SwayNodeDto)), nameof(SwayNodeDto.FloatingNodes)));
        Assert.Equal("window_properties", PropertyName(SwayJsonContext.Default.GetTypeInfo(typeof(SwayNodeDto)), nameof(SwayNodeDto.WindowProperties)));
        Assert.Equal("success", PropertyName(SwayJsonContext.Default.GetTypeInfo(typeof(SwayCommandResultDto)), nameof(SwayCommandResultDto.Success)));
        Assert.Equal("focusHistoryID", PropertyName(HyprlandJsonContext.Default.GetTypeInfo(typeof(HyprlandWindowDto)), nameof(HyprlandWindowDto.FocusHistoryId)));
        Assert.Equal("Ok", PropertyName(NiriJsonContext.Default.GetTypeInfo(typeof(NiriResponse<NiriWindowsData>)), nameof(NiriResponse<NiriWindowsData>.Ok)));
        Assert.Equal("FocusedWindow", PropertyName(NiriJsonContext.Default.GetTypeInfo(typeof(NiriFocusedWindowData)), nameof(NiriFocusedWindowData.FocusedWindow)));
        Assert.Equal("app_id", PropertyName(NiriJsonContext.Default.GetTypeInfo(typeof(NiriWindowDto)), nameof(NiriWindowDto.AppId)));
        Assert.Equal("window_size", PropertyName(NiriJsonContext.Default.GetTypeInfo(typeof(NiriLayoutDto)), nameof(NiriLayoutDto.WindowSize)));
        Assert.Equal("workspace_id", PropertyName(NiriJsonContext.Default.GetTypeInfo(typeof(NiriWindowDto)), nameof(NiriWindowDto.WorkspaceId)));
        Assert.Equal("is_focused", PropertyName(NiriJsonContext.Default.GetTypeInfo(typeof(NiriWorkspaceDto)), nameof(NiriWorkspaceDto.IsFocused)));
        Assert.Equal("Outputs", PropertyName(NiriJsonContext.Default.GetTypeInfo(typeof(NiriOutputsData)), nameof(NiriOutputsData.Outputs)));
        Assert.Equal("Pid", PropertyName(GnomeJsonContext.Default.GetTypeInfo(typeof(WindowInfo)), nameof(WindowInfo.Pid)));
        Assert.Equal("IsFocused", PropertyName(GnomeJsonContext.Default.GetTypeInfo(typeof(WindowInfo)), nameof(WindowInfo.IsFocused)));
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

        var swayWorkspace = JsonSerializer.Deserialize(
            "[{\"name\":\"main\",\"focused\":true}]",
            SwayJsonContext.Default.SwayWorkspaceDtoArray);
        var workspace = Assert.Single(swayWorkspace!);
        Assert.Equal("main", workspace.Name);
        Assert.True(workspace.Focused);

        var swayCommand = JsonSerializer.Deserialize(
            "[{\"success\":true}]",
            SwayJsonContext.Default.SwayCommandResultDtoArray);
        Assert.True(Assert.Single(swayCommand!).Success);

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

        var gnome = JsonSerializer.Deserialize(
            "{\"Address\":\"42\",\"Title\":\"Example\",\"Class\":\"org.example\",\"Pid\":0,\"Workspace\":\"1\",\"IsFocused\":true,\"IsFullscreen\":false,\"IsMaximized\":false,\"IsFloating\":true,\"IsPinned\":false,\"IsHidden\":false,\"X\":10,\"Y\":20,\"Width\":800,\"Height\":600}",
            GnomeJsonContext.Default.WindowInfo);
        Assert.NotNull(gnome);
        Assert.Equal("42", gnome.Address);
        Assert.Equal("org.example", gnome.Class);
        Assert.Equal(-1, gnome.Pid);
        Assert.True(gnome.IsFocused);
        Assert.Equal(800, gnome.Width);
    }

    [Fact]
    public void NiriRepresentativeFixtures_ShouldDeserializeNestedResponsesThroughGeneratedContexts()
    {
        var windows = JsonSerializer.Deserialize(
            "{\"Ok\":{\"Windows\":[{\"id\":9,\"title\":\"Example\",\"app_id\":\"org.example\",\"pid\":42,\"workspace_id\":7,\"is_focused\":true,\"is_floating\":false,\"is_urgent\":false,\"layout\":{\"window_size\":[800.5,600.25],\"tile_pos_in_workspace_view\":[10.5,-20.5]}}]}}",
            NiriJsonContext.Default.NiriResponseNiriWindowsData);
        Assert.NotNull(windows?.Ok?.Windows);
        var window = Assert.Single(windows.Ok.Windows);
        Assert.Equal(9UL, window.Id);
        Assert.Equal("org.example", window.AppId);
        Assert.Equal(7UL, window.WorkspaceId);
        Assert.Equal(800.5, window.Layout!.WindowSize![0]);
        Assert.Equal(-20.5, window.Layout.TilePosInWorkspaceView![1]);

        var workspaces = JsonSerializer.Deserialize(
            "{\"Ok\":{\"Workspaces\":[{\"id\":7,\"idx\":1,\"name\":\"main\",\"output\":\"DP-1\",\"is_urgent\":false,\"is_active\":true,\"is_focused\":true,\"active_window_id\":9}]}}",
            NiriJsonContext.Default.NiriResponseNiriWorkspacesData);
        var workspace = Assert.Single(workspaces!.Ok!.Workspaces!);
        Assert.Equal("main", workspace.Name);
        Assert.Equal("DP-1", workspace.Output);
        Assert.Equal(9UL, workspace.ActiveWindowId);

        var outputs = JsonSerializer.Deserialize(
            "{\"Ok\":{\"Outputs\":{\"DP-1\":{\"name\":\"DP-1\",\"logical\":{\"x\":-1920,\"y\":0,\"width\":1920,\"height\":1080}}}}}",
            NiriJsonContext.Default.NiriResponseNiriOutputsData);
        var output = outputs!.Ok!.Outputs!["DP-1"];
        Assert.Equal("DP-1", output.Name);
        Assert.Equal(-1920, output.Logical!.X);
        Assert.Equal(1920, output.Logical.Width);
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
