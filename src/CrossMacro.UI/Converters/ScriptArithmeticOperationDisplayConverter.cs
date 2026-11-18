namespace CrossMacro.UI.Converters;

/// <summary>
/// Maps an arithmetic operation to its culture-invariant glyph; intentionally not localized.
/// </summary>
public sealed class ScriptArithmeticOperationDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            ScriptArithmeticOperation.Add => "+",
            ScriptArithmeticOperation.Subtract => "−",
            ScriptArithmeticOperation.Multiply => "×",
            ScriptArithmeticOperation.Divide => "÷",
            ScriptArithmeticOperation.Modulo => "%",
            _ => System.Convert.ToString(value, culture) ?? string.Empty,
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
