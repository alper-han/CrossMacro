using System.Globalization;
using Avalonia.Data;
using CrossMacro.UI.Converters;
using FluentAssertions;

namespace CrossMacro.UI.Tests.Converters;

public class NumericUpDownValueConverterTests
{
    [Theory]
    [InlineData(typeof(int))]
    [InlineData(typeof(int?))]
    public void ConvertBack_WhenNumericUpDownValueIsCleared_DoesNotUpdateSource(Type targetType)
    {
        var result = NumericUpDownValueConverter.Instance.ConvertBack(null, targetType, null, CultureInfo.InvariantCulture);

        result.Should().BeSameAs(BindingOperations.DoNothing);
    }

    [Theory]
    [InlineData(typeof(int))]
    [InlineData(typeof(int?))]
    public void ConvertBack_WhenTargetIsInteger_ReturnsTruncatedInteger(Type targetType)
    {
        var result = NumericUpDownValueConverter.Instance.ConvertBack(42.9m, targetType, null, CultureInfo.InvariantCulture);

        result.Should().Be(42);
    }

    [Fact]
    public void Convert_WhenSourceIsInteger_ReturnsDecimalForNumericUpDown()
    {
        var result = NumericUpDownValueConverter.Instance.Convert(7, typeof(decimal?), null, CultureInfo.InvariantCulture);

        result.Should().Be(7m);
    }

    [Fact]
    public void ConvertBack_WhenValueIsNotDecimal_DoesNotUpdateSource()
    {
        var result = NumericUpDownValueConverter.Instance.ConvertBack("", typeof(int), null, CultureInfo.InvariantCulture);

        result.Should().BeSameAs(BindingOperations.DoNothing);
    }

    [Theory]
    [InlineData("2147483648")]
    [InlineData("-2147483649")]
    public void ConvertBack_WhenIntegerValueIsOutOfRange_DoesNotUpdateSource(string value)
    {
        var decimalValue = decimal.Parse(value, CultureInfo.InvariantCulture);

        var result = NumericUpDownValueConverter.Instance.ConvertBack(decimalValue, typeof(int), null, CultureInfo.InvariantCulture);

        result.Should().BeSameAs(BindingOperations.DoNothing);
    }
}
