
namespace CrossMacro.UI.Tests.Converters;

public sealed class NumericUpDownValueConverterTests
{
    [Theory]
    [InlineData(typeof(int))]
    [InlineData(typeof(int?))]
    public void ConvertBack_WhenNumericUpDownValueIsCleared_DoesNotUpdateSource(Type targetType)
    {
        var result = NumericUpDownValueConverter.Instance.ConvertBack(value: null, targetType, parameter: null, CultureInfo.InvariantCulture);

        _ = result.Should().BeSameAs(BindingOperations.DoNothing);
    }

    [Theory]
    [InlineData(typeof(int))]
    [InlineData(typeof(int?))]
    public void ConvertBack_WhenTargetIsInteger_ReturnsTruncatedInteger(Type targetType)
    {
        var result = NumericUpDownValueConverter.Instance.ConvertBack(42.9m, targetType, parameter: null, CultureInfo.InvariantCulture);

        _ = result.Should().Be(42);
    }

    [Fact]
    public void Convert_WhenSourceIsInteger_ReturnsDecimalForNumericUpDown()
    {
        var result = NumericUpDownValueConverter.Instance.Convert(7, typeof(decimal?), parameter: null, CultureInfo.InvariantCulture);

        _ = result.Should().Be(7m);
    }

    [Fact]
    public void ConvertBack_WhenValueIsNotDecimal_DoesNotUpdateSource()
    {
        var result = NumericUpDownValueConverter.Instance.ConvertBack("", typeof(int), parameter: null, CultureInfo.InvariantCulture);

        _ = result.Should().BeSameAs(BindingOperations.DoNothing);
    }

    [Theory]
    [InlineData("2147483648")]
    [InlineData("-2147483649")]
    public void ConvertBack_WhenIntegerValueIsOutOfRange_DoesNotUpdateSource(string value)
    {
        var decimalValue = decimal.Parse(value, CultureInfo.InvariantCulture);

        var result = NumericUpDownValueConverter.Instance.ConvertBack(decimalValue, typeof(int), parameter: null, CultureInfo.InvariantCulture);

        _ = result.Should().BeSameAs(BindingOperations.DoNothing);
    }
}
