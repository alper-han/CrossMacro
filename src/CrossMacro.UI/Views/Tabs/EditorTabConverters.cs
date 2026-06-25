using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.UI.Localization;

namespace CrossMacro.UI.Views.Tabs;

/// <summary>
/// Converters for EditorActionType visibility in the editor UI.
/// </summary>
public static class ActionTypeConverters
{
    private static EditorActionDisplayFormatter? _formatter;

    public static void Configure(EditorActionDisplayFormatter formatter)
    {
        _formatter = formatter;
    }

    /// <summary>
    /// Returns true if the action type is a mouse-related action.
    /// </summary>
    public static readonly IValueConverter IsMouseAction = new FuncValueConverter<EditorActionType, bool>(type =>
        type is EditorActionType.MouseMove 
            or EditorActionType.MouseClick 
            or EditorActionType.MouseDown 
            or EditorActionType.MouseUp);
    
    /// <summary>
    /// Returns true if the action type is a click action.
    /// </summary>
    public static readonly IValueConverter IsClickAction = new FuncValueConverter<EditorActionType, bool>(type =>
        type is EditorActionType.MouseClick 
            or EditorActionType.MouseDown 
            or EditorActionType.MouseUp);
    
    /// <summary>
    /// Returns true if the action type is a keyboard action.
    /// </summary>
    public static readonly IValueConverter IsKeyAction = new FuncValueConverter<EditorActionType, bool>(type =>
        type is EditorActionType.KeyPress 
            or EditorActionType.KeyDown 
            or EditorActionType.KeyUp);
    
    /// <summary>
    /// Returns true if the action type is a scroll action.
    /// </summary>
    public static readonly IValueConverter IsScrollAction = new FuncValueConverter<EditorActionType, bool>(type =>
        type is EditorActionType.ScrollVertical 
            or EditorActionType.ScrollHorizontal);

    public static readonly IValueConverter DisplayText = new FuncValueConverter<EditorActionType, string>(type =>
    {
        return _formatter?.FormatActionType(type) ?? type.ToString();
    });
}

public static class ScheduleTaskConverters
{
    private static ILocalizationService? _localizationService;

    public static void Configure(ILocalizationService localizationService)
    {
        _localizationService = localizationService;
    }

    public static readonly IValueConverter SummaryText = new FuncValueConverter<ScheduledTask?, string>(task =>
    {
        if (task is null)
        {
            return string.Empty;
        }

        var localizationService = _localizationService;
        if (localizationService is null)
        {
            var fileName = string.IsNullOrEmpty(task.MacroFilePath) ? "No file" : System.IO.Path.GetFileName(task.MacroFilePath);
            return $"{task.Type} • {fileName}";
        }

        var typeDisplay = task.Type switch
        {
            ScheduleType.Interval => localizationService["Schedule_TypeInterval"],
            ScheduleType.SpecificTime => localizationService["Schedule_TypeDateTime"],
            ScheduleType.Weekly => localizationService["Schedule_TypeWeekly"],
            _ => task.Type.ToString()
        };

        var fileDisplay = string.IsNullOrEmpty(task.MacroFilePath)
            ? localizationService["Schedule_NoFile"]
            : System.IO.Path.GetFileName(task.MacroFilePath);

        return string.Format(localizationService.CurrentCulture, localizationService["Schedule_ListSummary"], typeDisplay, fileDisplay);
    });

}

public static class EditorScriptDisplayConverters
{
    private static ILocalizationService? _localizationService;

    public static void Configure(ILocalizationService localizationService)
    {
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
    }

    public static string FormatOperandType(ScriptOperandType operandType)
    {
        return operandType switch
        {
            ScriptOperandType.VariableReference => Localize("Editor_ScriptOperand_VariableReference", "Variable"),
            ScriptOperandType.Number => Localize("Editor_ScriptOperand_Number", "Number"),
            ScriptOperandType.Text => Localize("Editor_ScriptOperand_Text", "Text"),
            ScriptOperandType.Boolean => Localize("Editor_ScriptOperand_Boolean", "True / False"),
            ScriptOperandType.Color => Localize("Editor_ScriptOperand_Color", "Color (RRGGBB)"),
            _ => operandType.ToString()
        };
    }

    public static string FormatConditionOperator(ScriptConditionOperator conditionOperator)
    {
        return conditionOperator switch
        {
            ScriptConditionOperator.Equals => Localize("Editor_ScriptConditionOperator_Equals", "Equals (=)"),
            ScriptConditionOperator.NotEquals => Localize("Editor_ScriptConditionOperator_NotEquals", "Not equals (!=)"),
            ScriptConditionOperator.GreaterThan => Localize("Editor_ScriptConditionOperator_GreaterThan", "Greater than (>)"),
            ScriptConditionOperator.GreaterThanOrEqual => Localize("Editor_ScriptConditionOperator_GreaterThanOrEqual", "Greater than or equal (>=)"),
            ScriptConditionOperator.LessThan => Localize("Editor_ScriptConditionOperator_LessThan", "Less than (<)"),
            ScriptConditionOperator.LessThanOrEqual => Localize("Editor_ScriptConditionOperator_LessThanOrEqual", "Less than or equal (<=)"),
            _ => conditionOperator.ToString()
        };
    }

    private static string Localize(string key, string fallback)
    {
        var localized = _localizationService?[key];
        return string.IsNullOrWhiteSpace(localized) ? fallback : localized;
    }
}

public static class EditorScreenTargetColorSourceDisplayConverters
{
    private static ILocalizationService? _localizationService;

    public static void Configure(ILocalizationService localizationService)
    {
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
    }

    public static string FormatSource(EditorActionScreenTargetColorSource source)
    {
        return source switch
        {
            EditorActionScreenTargetColorSource.Variable => Localize("Editor_TargetColorSourceVariable", "Variable"),
            _ => Localize("Editor_TargetColorSourceManualHex", "Manual hex")
        };
    }

    private static string Localize(string key, string fallback)
    {
        var localized = _localizationService?[key];
        return string.IsNullOrWhiteSpace(localized) ? fallback : localized;
    }
}

public class ScreenTargetColorSourceDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            EditorActionScreenTargetColorSource source => EditorScreenTargetColorSourceDisplayConverters.FormatSource(source),
            _ => value?.ToString() ?? string.Empty
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Converter to get index of item in list.
/// </summary>
public class IndexConverter : IValueConverter
{
    public static readonly IndexConverter Instance = new();
    
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // This is a placeholder - actual implementation would need list context
        return "•";
    }
    
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Converter for int properties that handles empty/invalid string input gracefully.
/// Empty string = 0, invalid text = keeps previous value (DoNothing).
/// </summary>
public class NullableIntConverter : IValueConverter
{
    public static readonly NullableIntConverter Instance = new();
    
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int intValue)
        {
            return intValue.ToString();
        }
        return value?.ToString() ?? "";
    }
    
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string str)
        {
            // Empty string = 0 (explicit clear)
            if (string.IsNullOrWhiteSpace(str))
                return 0;
            
            // Valid number = use it (clamped for key codes if needed)
            if (int.TryParse(str, out int result))
            {
                return result;
            }
            
            // Invalid text (like "a") = don't update, keep previous value
            return Avalonia.Data.BindingOperations.DoNothing;
        }
        return Avalonia.Data.BindingOperations.DoNothing;
    }
}

public class HexColorBrushConverter : IValueConverter
{
    public static readonly HexColorBrushConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var text = value?.ToString()?.Trim();
        if (text is { Length: 6 } && uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
        {
            var red = (byte)((rgb >> 16) & 0xFF);
            var green = (byte)((rgb >> 8) & 0xFF);
            var blue = (byte)(rgb & 0xFF);
            return new SolidColorBrush(Color.FromRgb(red, green, blue));
        }

        return Brushes.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public class ScriptOperandTypeDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            ScriptOperandType operandType => EditorScriptDisplayConverters.FormatOperandType(operandType),
            _ => value?.ToString() ?? string.Empty
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public class ScriptConditionOperatorDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            ScriptConditionOperator conditionOperator => EditorScriptDisplayConverters.FormatConditionOperator(conditionOperator),
            _ => value?.ToString() ?? string.Empty
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Converts block indent level into left margin for the action list.
/// </summary>
public class IndentLevelToMarginConverter : IValueConverter
{
    public static readonly IndentLevelToMarginConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int indentLevel && indentLevel > 0)
        {
            return new Thickness(indentLevel * 14, 0, 0, 0);
        }

        return new Thickness(0);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
