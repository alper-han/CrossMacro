namespace CrossMacro.UI.Tests.Views.Tabs;

using System.Globalization;
using Avalonia.Data;
using CrossMacro.Core.Models;
using CrossMacro.UI.Views.Tabs;

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
}
