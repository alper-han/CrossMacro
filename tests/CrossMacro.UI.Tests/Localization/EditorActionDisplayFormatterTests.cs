using System.Globalization;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.UI.Localization;
using FluentAssertions;
using NSubstitute;

namespace CrossMacro.UI.Tests.Localization;

public class EditorActionDisplayFormatterTests
{
    [Theory]
    [InlineData(EditorActionType.PixelColor, "Editor_ActionType_PixelColor", "Pixel Color")]
    [InlineData(EditorActionType.WaitColor, "Editor_ActionType_WaitColor", "Wait Color")]
    [InlineData(EditorActionType.PixelSearch, "Editor_ActionType_PixelSearch", "Pixel Search")]
    public void FormatActionType_ForScreenReadingActions_UsesLocalizedLabels(
        EditorActionType actionType,
        string resourceKey,
        string expected)
    {
        var formatter = CreateFormatter(resourceKey, expected);

        formatter.FormatActionType(actionType).Should().Be(expected);
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
            ScreenColorVariableName = "sample"
        };

        formatter.Format(action).Should().Be("Pixel color (10, 20) -> sample");
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
            ScreenColorVariableName = "sample"
        };

        formatter.Format(action).Should().Be("Pixel color rel (+5, -3) -> sample");
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
            ScreenColorVariableName = "wait_ok"
        };

        formatter.Format(action).Should().Be("Wait for 12ABEF at (30, 40) up to 2500ms -> wait_ok");
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
            ScreenTolerance = 26
        };

        formatter.Format(action).Should().Be("Find 00FF11 in (1, 2, 300x200) -> found, hit_x, hit_y tol 26");
    }

    private static EditorActionDisplayFormatter CreateFormatter(string resourceKey, string resourceValue)
    {
        var localizationService = Substitute.For<ILocalizationService>();
        localizationService.CurrentCulture.Returns(CultureInfo.InvariantCulture);
        localizationService[Arg.Any<string>()].Returns(call => call.Arg<string>() == resourceKey ? resourceValue : call.Arg<string>());
        return new EditorActionDisplayFormatter(localizationService);
    }
}
