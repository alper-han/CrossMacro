namespace CrossMacro.UI.Tests.Views.Tabs;


public sealed class EditorTabConvertersTests
{
    [Fact]
    public void ActionTypeConverters_ShouldClassifyActionsCorrectly()
    {
        var culture = CultureInfo.InvariantCulture;

        Assert.True((bool)ActionTypeConverters.IsMouseAction.Convert(EditorActionType.MouseMove, typeof(bool), parameter: null, culture)!);
        Assert.False((bool)ActionTypeConverters.IsMouseAction.Convert(EditorActionType.Delay, typeof(bool), parameter: null, culture)!);

        Assert.True((bool)ActionTypeConverters.IsClickAction.Convert(EditorActionType.MouseDown, typeof(bool), parameter: null, culture)!);
        Assert.False((bool)ActionTypeConverters.IsClickAction.Convert(EditorActionType.KeyPress, typeof(bool), parameter: null, culture)!);

        Assert.True((bool)ActionTypeConverters.IsKeyAction.Convert(EditorActionType.KeyUp, typeof(bool), parameter: null, culture)!);
        Assert.False((bool)ActionTypeConverters.IsKeyAction.Convert(EditorActionType.MouseUp, typeof(bool), parameter: null, culture)!);

        Assert.True((bool)ActionTypeConverters.IsScrollAction.Convert(EditorActionType.ScrollHorizontal, typeof(bool), parameter: null, culture)!);
        Assert.False((bool)ActionTypeConverters.IsScrollAction.Convert(EditorActionType.MouseMove, typeof(bool), parameter: null, culture)!);
    }

    [Fact]
    public void NullableIntConverter_ShouldHandleValidEmptyAndInvalidInputs()
    {
        var converter = new NullableIntConverter();
        var culture = CultureInfo.InvariantCulture;

        Assert.Equal("42", converter.Convert(42, typeof(string), parameter: null, culture));
        var emptyResult = Assert.IsType<string>(converter.Convert(value: null, typeof(string), parameter: null, culture));
        Assert.Empty(emptyResult);

        Assert.Equal(0, converter.ConvertBack("", typeof(int), parameter: null, culture));
        Assert.Equal(17, converter.ConvertBack("17", typeof(int), parameter: null, culture));
        Assert.Same(BindingOperations.DoNothing, converter.ConvertBack("abc", typeof(int), parameter: null, culture));
        Assert.Same(BindingOperations.DoNothing, converter.ConvertBack(99, typeof(int), parameter: null, culture));
    }

    [Theory]
    [InlineData("12ABEF", 0x12, 0xAB, 0xEF)]
    [InlineData("12abef", 0x12, 0xAB, 0xEF)]
    [InlineData(" 0055AA ", 0x00, 0x55, 0xAA)]
    public void HexColorBrushConverter_ForValidRgbHex_ReturnsMatchingBrush(string hex, byte red, byte green, byte blue)
    {
        var converter = new HexColorBrushConverter();

        var result = converter.Convert(hex, typeof(IBrush), parameter: null, CultureInfo.InvariantCulture);

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

        var result = converter.Convert(hex, typeof(IBrush), parameter: null, CultureInfo.InvariantCulture);

        Assert.Same(Brushes.Transparent, result);
    }
}
