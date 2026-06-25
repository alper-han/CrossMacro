namespace CrossMacro.UI.Tests.Views.Tabs;

using System.Globalization;
using Avalonia.Data;
using Avalonia.Media;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.UI.Localization;
using CrossMacro.UI.Views.Tabs;
using NSubstitute;

public class EditorTabConvertersTests
{
    [Fact]
    public void ActionTypeConverters_ShouldClassifyActionsCorrectly()
    {
        var culture = CultureInfo.InvariantCulture;

        Assert.True((bool)ActionTypeConverters.IsMouseAction.Convert(EditorActionType.MouseMove, typeof(bool), null, culture)!);
        Assert.False((bool)ActionTypeConverters.IsMouseAction.Convert(EditorActionType.Delay, typeof(bool), null, culture)!);

        Assert.True((bool)ActionTypeConverters.IsClickAction.Convert(EditorActionType.MouseDown, typeof(bool), null, culture)!);
        Assert.False((bool)ActionTypeConverters.IsClickAction.Convert(EditorActionType.KeyPress, typeof(bool), null, culture)!);

        Assert.True((bool)ActionTypeConverters.IsKeyAction.Convert(EditorActionType.KeyUp, typeof(bool), null, culture)!);
        Assert.False((bool)ActionTypeConverters.IsKeyAction.Convert(EditorActionType.MouseUp, typeof(bool), null, culture)!);

        Assert.True((bool)ActionTypeConverters.IsScrollAction.Convert(EditorActionType.ScrollHorizontal, typeof(bool), null, culture)!);
        Assert.False((bool)ActionTypeConverters.IsScrollAction.Convert(EditorActionType.MouseMove, typeof(bool), null, culture)!);
    }

    [Fact]
    public void IndexConverter_ShouldReturnBullet_AndThrowOnConvertBack()
    {
        var converter = new IndexConverter();
        var culture = CultureInfo.InvariantCulture;

        var value = converter.Convert(value: 123, targetType: typeof(string), parameter: null, culture);

        Assert.Equal("•", value);
        Assert.Throws<NotSupportedException>(() => converter.ConvertBack("1", typeof(int), null, culture));
    }

    [Fact]
    public void NullableIntConverter_ShouldHandleValidEmptyAndInvalidInputs()
    {
        var converter = new NullableIntConverter();
        var culture = CultureInfo.InvariantCulture;

        Assert.Equal("42", converter.Convert(42, typeof(string), null, culture));
        Assert.Equal("", converter.Convert(null, typeof(string), null, culture));

        Assert.Equal(0, converter.ConvertBack("", typeof(int), null, culture));
        Assert.Equal(17, converter.ConvertBack("17", typeof(int), null, culture));
        Assert.Same(BindingOperations.DoNothing, converter.ConvertBack("abc", typeof(int), null, culture));
        Assert.Same(BindingOperations.DoNothing, converter.ConvertBack(99, typeof(int), null, culture));
    }

    [Theory]
    [InlineData("12ABEF", 0x12, 0xAB, 0xEF)]
    [InlineData("12abef", 0x12, 0xAB, 0xEF)]
    [InlineData(" 0055AA ", 0x00, 0x55, 0xAA)]
    public void HexColorBrushConverter_ForValidRgbHex_ReturnsMatchingBrush(string hex, byte red, byte green, byte blue)
    {
        var converter = new HexColorBrushConverter();

        var result = converter.Convert(hex, typeof(IBrush), null, CultureInfo.InvariantCulture);

        var brush = Assert.IsType<SolidColorBrush>(result);
        Assert.Equal(Color.FromRgb(red, green, blue), brush.Color);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("GGGGGG")]
    public void HexColorBrushConverter_ForInvalidHex_ReturnsTransparentBrush(string? hex)
    {
        var converter = new HexColorBrushConverter();

        var result = converter.Convert(hex, typeof(IBrush), null, CultureInfo.InvariantCulture);

        Assert.Same(Brushes.Transparent, result);
    }

    [Fact]
    public void ScriptOperandTypeDisplayConverter_ShouldUseSeparateTextAndColorLabels()
    {
        var localizationService = Substitute.For<ILocalizationService>();
        localizationService["Editor_ScriptOperand_Text"].Returns("[Editor_ScriptOperand_Text]");
        localizationService["Editor_ScriptOperand_Color"].Returns("[Editor_ScriptOperand_Color]");
        EditorScriptDisplayConverters.Configure(localizationService);
        var converter = new ScriptOperandTypeDisplayConverter();

        var textResult = converter.Convert(ScriptOperandType.Text, typeof(string), null, CultureInfo.InvariantCulture);
        var colorResult = converter.Convert(ScriptOperandType.Color, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.Equal("[Editor_ScriptOperand_Text]", textResult);
        Assert.Equal("[Editor_ScriptOperand_Color]", colorResult);
    }

    [Fact]
    public void ScriptConditionOperatorDisplayConverter_ShouldUseFriendlyLabels()
    {
        var localizationService = Substitute.For<ILocalizationService>();
        localizationService["Editor_ScriptConditionOperator_GreaterThanOrEqual"].Returns("[Editor_ScriptConditionOperator_GreaterThanOrEqual]");
        EditorScriptDisplayConverters.Configure(localizationService);
        var converter = new ScriptConditionOperatorDisplayConverter();

        var result = converter.Convert(ScriptConditionOperator.GreaterThanOrEqual, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.Equal("[Editor_ScriptConditionOperator_GreaterThanOrEqual]", result);
    }

    [Fact]
    public void ActionTypeConverters_DisplayText_UsesConfiguredFormatter()
    {
        var localizationService = Substitute.For<ILocalizationService>();
        localizationService["Editor_ActionType_MouseClick"].Returns("[Editor_ActionType_MouseClick]");
        var formatter = new EditorActionDisplayFormatter(localizationService);

        ActionTypeConverters.Configure(formatter);

        var result = ActionTypeConverters.DisplayText.Convert(EditorActionType.MouseClick, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.Equal("[Editor_ActionType_MouseClick]", result);
    }

    [Fact]
    public void ScheduleTaskConverters_SummaryText_UsesConfiguredLocalizationService()
    {
        var localizationService = Substitute.For<ILocalizationService>();
        localizationService.CurrentCulture.Returns(CultureInfo.InvariantCulture);
        localizationService["Schedule_TypeInterval"].Returns("[Schedule_TypeInterval]");
        localizationService["Schedule_TypeWeekly"].Returns("[Schedule_TypeWeekly]");
        localizationService["Schedule_NoFile"].Returns("[Schedule_NoFile]");
        localizationService["Schedule_ListSummary"].Returns("[Schedule_ListSummary] {0} | {1}");
        ScheduleTaskConverters.Configure(localizationService);

        var task = new ScheduledTask { Type = ScheduleType.Weekly, MacroFilePath = string.Empty };

        var result = ScheduleTaskConverters.SummaryText.Convert(task, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.Equal("[Schedule_ListSummary] [Schedule_TypeWeekly] | [Schedule_NoFile]", result);
    }

}
