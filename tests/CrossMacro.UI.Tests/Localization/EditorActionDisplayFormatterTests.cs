
namespace CrossMacro.UI.Tests.Localization;

public sealed class EditorActionDisplayFormatterTests
{
    [Theory]
    [InlineData(EditorActionType.PixelColor, "Editor_ActionType_PixelColor", "Pixel Color")]
    [InlineData(EditorActionType.WaitColor, "Editor_ActionType_WaitColor", "Wait Color")]
    [InlineData(EditorActionType.PixelSearch, "Editor_ActionType_PixelSearch", "Pixel Search")]
    [InlineData(EditorActionType.ImageClick, "Editor_ActionType_ImageClick", "Image Click")]
    [InlineData(EditorActionType.WaitImage, "Editor_ActionType_WaitImage", "Wait Image")]
    [InlineData(EditorActionType.ClipboardGet, "Editor_ActionType_ClipboardGet", "Clipboard Get")]
    [InlineData(EditorActionType.ClipboardSet, "Editor_ActionType_ClipboardSet", "Clipboard Set")]
    [InlineData(EditorActionType.ShellCommand, "Editor_ActionType_ShellCommand", "Shell Command")]
    [InlineData(EditorActionType.Screenshot, "Editor_ActionType_Screenshot", "Screenshot")]
    public void FormatActionType_UsesLocalizedLabels(
        EditorActionType actionType,
        string resourceKey,
        string expected)
    {
        var formatter = CreateFormatter(resourceKey, expected);

        _ = formatter.FormatActionType(actionType).Should().Be(expected);
    }

    [Fact]
    public void Format_ForClipboardGet_IncludesDestinationVariable()
    {
        var formatter = CreateFormatter("Editor_Action_ClipboardGet", "Read clipboard into {0}");
        var action = new EditorAction { Type = EditorActionType.ClipboardGet, ScriptVariableName = "clipText" };

        _ = formatter.Format(action).Should().Be("Read clipboard into clipText");
    }

    [Fact]
    public void Format_ForClipboardSet_IncludesTextPreview()
    {
        var formatter = CreateFormatter("Editor_Action_ClipboardSet", "Set clipboard to \"{0}\"");
        var action = new EditorAction { Type = EditorActionType.ClipboardSet, Text = "hello" };

        _ = formatter.Format(action).Should().Be("Set clipboard to \"hello\"");
    }

    [Fact]
    public void Format_ForShellCaptureInput_IncludesCommandAndCaptureTargets()
    {
        var formatter = CreateFormatter("Editor_Action_ShellCaptureInput", "Run shell with input \"{0}\" -> {1}, {2}, {3}");
        var action = new EditorAction
        {
            Type = EditorActionType.ShellCommand,
            ShellCommandMode = ShellCommandMode.ShellCaptureInput,
            ShellCommand = "cat",
            ShellExitCodeVariableName = "exitCode",
            ShellStandardOutputVariableName = "stdout",
            ShellStandardErrorVariableName = "_",
        };

        _ = formatter.Format(action).Should().Be("Run shell with input \"cat\" -> exitCode, stdout, _");
    }

    [Fact]
    public void Format_ForScreenshotClipboard_UsesClipboardDestination()
    {
        var formatter = CreateFormatter(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Editor_Action_Screenshot"] = "Screenshot -> {0}",
            ["Editor_ScreenshotClipboardDestination"] = "clipboard",
        });
        var action = new EditorAction { Type = EditorActionType.Screenshot, ScreenshotCopyToClipboard = true };

        _ = formatter.Format(action).Should().Be("Screenshot -> clipboard");
    }

    [Fact]
    public void Format_ForScreenshotRegionFileAndClipboard_IncludesRegionAndDestination()
    {
        var formatter = CreateFormatter(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Editor_Action_ScreenshotRegion"] = "Screenshot ({0}, {1}, {2}x{3}) -> {4}",
            ["Editor_ScreenshotFileAndClipboardDestination"] = "{0} + clipboard",
        });
        var action = new EditorAction
        {
            Type = EditorActionType.Screenshot,
            ScreenshotOutputPath = "./shot.png",
            ScreenshotCopyToClipboard = true,
            ScreenshotUseRegion = true,
            ScreenshotRegionX = "10",
            ScreenshotRegionY = "20",
            ScreenshotRegionWidth = "300",
            ScreenshotRegionHeight = "200",
        };

        _ = formatter.Format(action).Should().Be("Screenshot (10, 20, 300x200) -> ./shot.png + clipboard");
    }

    [Fact]
    public void Format_ForAbsolutePixelColor_UsesStructuredScreenFields()
    {
        var formatter = CreateFormatter("Editor_Action_PixelColorAbsolute", "Pixel color ({0}, {1}) -> {2}");
        var action = new EditorAction
        {
            Type = EditorActionType.PixelColor,
            IsAbsolute = true,
            ScreenX = 10,
            ScreenY = 20,
            ScreenColorVariableName = "sample",
        };

        _ = formatter.Format(action).Should().Be("Pixel color (10, 20) -> sample");
    }

    [Fact]
    public void Format_ForRelativePixelColor_UsesRelativeLabel()
    {
        var formatter = CreateFormatter("Editor_Action_PixelColorRelative", "Pixel color rel ({0:+#;-#;0}, {1:+#;-#;0}) -> {2}");
        var action = new EditorAction
        {
            Type = EditorActionType.PixelColor,
            IsAbsolute = false,
            ScreenX = 5,
            ScreenY = -3,
            ScreenColorVariableName = "sample",
        };

        _ = formatter.Format(action).Should().Be("Pixel color rel (+5, -3) -> sample");
    }

    [Fact]
    public void Format_ForWaitColor_IncludesColorPointAndTimeout()
    {
        var formatter = CreateFormatter("Editor_Action_WaitColor", "Wait for {0} at ({1}, {2}) up to {3}ms -> {4}");
        var action = new EditorAction
        {
            Type = EditorActionType.WaitColor,
            ScreenX = 30,
            ScreenY = 40,
            ScreenColorHex = "12ABEF",
            ScreenTimeoutMs = 2500,
            ScreenColorVariableName = "wait_ok",
        };

        _ = formatter.Format(action).Should().Be("Wait for 12ABEF at (30, 40) up to 2500ms -> wait_ok");
    }

    [Fact]
    public void Format_ForPixelSearch_IncludesColorAndRegion()
    {
        var formatter = CreateFormatter("Editor_Action_PixelSearch", "Find {0} in ({1}, {2}, {3}x{4}) -> {5}, {6}, {7} tol {8}");
        var action = new EditorAction
        {
            Type = EditorActionType.PixelSearch,
            ScreenLeft = 1,
            ScreenTop = 2,
            ScreenWidth = 300,
            ScreenHeight = 200,
            ScreenColorHex = "00FF11",
            ScreenFoundVariableName = "found",
            ScreenFoundXVariableName = "hit_x",
            ScreenFoundYVariableName = "hit_y",
            ScreenTolerance = 26,
        };

        _ = formatter.Format(action).Should().Be("Find 00FF11 in (1, 2, 300x200) -> found, hit_x, hit_y tol 26");
    }

    [Fact]
    public void Format_ForImageClick_UsesLocalizedResourceAndPreservesFields()
    {
        var formatter = CreateFormatter("Editor_Action_ImageClick", "Click image {0} in ({1}, {2}, {3}x{4})");
        var action = new EditorAction
        {
            Type = EditorActionType.ImageClick,
            ImageAssetName = "ButtonAsset",
            ScreenLeft = 10,
            ScreenTop = 20,
            ScreenWidth = 300,
            ScreenHeight = 200,
        };

        _ = formatter.Format(action).Should().Be("Click image ButtonAsset in (10, 20, 300x200)");
    }

    [Fact]
    public void Format_ForWaitImage_UsesLocalizedResourceAndPreservesTimeout()
    {
        var formatter = CreateFormatter("Editor_Action_WaitImage", "Wait for image {0} in ({1}, {2}, {3}x{4}) up to {5}ms");
        var action = new EditorAction
        {
            Type = EditorActionType.WaitImage,
            ImageAssetName = "ReadyAsset",
            ScreenLeft = 10,
            ScreenTop = 20,
            ScreenWidth = 300,
            ScreenHeight = 200,
            ScreenTimeoutMs = 2500,
        };

        _ = formatter.Format(action).Should().Be("Wait for image ReadyAsset in (10, 20, 300x200) up to 2500ms");
    }

    [Fact]
    public void FormatActionType_ForWindowCommand_UsesLocalizedLabel()
    {
        var formatter = CreateFormatter("Editor_ActionType_WindowCommand", "Window Command");

        _ = formatter.FormatActionType(EditorActionType.WindowCommand).Should().Be("Window Command");
    }

    [Theory]
    [InlineData(WindowCommandMode.Active, "Get active window title -> activeTitle")]
    [InlineData(WindowCommandMode.Search, "Search window by title \"Firefox\" -> addr")]
    [InlineData(WindowCommandMode.Wait, "Wait for window title \"Firefox\" (2500ms) -> addr")]
    [InlineData(WindowCommandMode.Focus, "Focus window by title \"Firefox\"")]
    [InlineData(WindowCommandMode.Close, "Close window by title \"Firefox\"")]
    [InlineData(WindowCommandMode.Move, "Move active window to 10, 20")]
    [InlineData(WindowCommandMode.Resize, "Resize active window to 800x600")]
    [InlineData(WindowCommandMode.Center, "Center active window")]
    [InlineData(WindowCommandMode.Maximize, "Maximize active window")]
    [InlineData(WindowCommandMode.Fullscreen, "Fullscreen active window")]
    [InlineData(WindowCommandMode.Floating, "Float active window")]
    [InlineData(WindowCommandMode.WorkspaceGet, "Get active workspace -> workspace")]
    [InlineData(WindowCommandMode.WorkspaceSwitch, "Switch to workspace 2")]
    [InlineData(WindowCommandMode.WorkspaceMoveActive, "Move active window to workspace 2")]
    [InlineData(WindowCommandMode.WorkspaceMoveWindow, "Move window 0x123 to workspace 2")]
    public void Format_ForWindowCommand_UsesModeSpecificResources(WindowCommandMode mode, string expected)
    {
        var formatter = CreateFormatter(WindowResources());

        _ = formatter.Format(CreateWindowAction(mode)).Should().Be(expected);
    }

    private static Dictionary<string, string> WindowResources()
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Editor_Action_WindowActive"] = "Get active window {0} -> {1}",
            ["Editor_Action_WindowSearch"] = "Search window by {0} \"{1}\" -> {2}",
            ["Editor_Action_WindowWait"] = "Wait for window {0} \"{1}\" ({2}ms) -> {3}",
            ["Editor_Action_WindowFocus"] = "Focus window by {0} \"{1}\"",
            ["Editor_Action_WindowFocusActive"] = "Focus active window",
            ["Editor_Action_WindowClose"] = "Close window by {0} \"{1}\"",
            ["Editor_Action_WindowCloseActive"] = "Close active window",
            ["Editor_Action_WindowMove"] = "Move active window to {0}, {1}",
            ["Editor_Action_WindowResize"] = "Resize active window to {0}x{1}",
            ["Editor_Action_WindowCenter"] = "Center active window",
            ["Editor_Action_WindowMaximize"] = "Maximize active window",
            ["Editor_Action_WindowFullscreen"] = "Fullscreen active window",
            ["Editor_Action_WindowFloat"] = "Float active window",
            ["Editor_Action_WindowWorkspaceGet"] = "Get active workspace -> {0}",
            ["Editor_Action_WindowWorkspaceSwitch"] = "Switch to workspace {0}",
            ["Editor_Action_WindowWorkspaceMoveActive"] = "Move active window to workspace {0}",
            ["Editor_Action_WindowWorkspaceMoveWindow"] = "Move window {0} to workspace {1}",
        };
    }

    private static EditorAction CreateWindowAction(WindowCommandMode mode)
    {
        return new EditorAction
        {
            Type = EditorActionType.WindowCommand,
            WindowCommandMode = mode,
            WindowSelectorKind = "title",
            WindowSelectorValue = mode is WindowCommandMode.WorkspaceMoveWindow ? "0x123" : "Firefox",
            WindowActiveField = "title",
            WindowOutputVariable = mode switch
            {
                WindowCommandMode.Active => "activeTitle",
                WindowCommandMode.WorkspaceGet => "workspace",
                _ => "addr",
            },
            WindowTimeoutMs = 2500,
            WindowX = 10,
            WindowY = 20,
            WindowWidth = 800,
            WindowHeight = 600,
            WindowWorkspace = "2",
        };
    }

    private static EditorActionDisplayFormatter CreateFormatter(string resourceKey, string resourceValue)
    {
        var localizationService = Substitute.For<ILocalizationService>();
        _ = localizationService.CurrentCulture.Returns(CultureInfo.InvariantCulture);
        _ = localizationService[Arg.Any<string>()].Returns(call => string.Equals(call.Arg<string>(), resourceKey, StringComparison.Ordinal) ? resourceValue : call.Arg<string>());
        return new EditorActionDisplayFormatter(localizationService);
    }

    private static EditorActionDisplayFormatter CreateFormatter(IReadOnlyDictionary<string, string> resources)
    {
        var localizationService = Substitute.For<ILocalizationService>();
        _ = localizationService.CurrentCulture.Returns(CultureInfo.InvariantCulture);
        _ = localizationService[Arg.Any<string>()].Returns(call => resources.GetValueOrDefault(call.Arg<string>(), call.Arg<string>()));
        return new EditorActionDisplayFormatter(localizationService);
    }
}
