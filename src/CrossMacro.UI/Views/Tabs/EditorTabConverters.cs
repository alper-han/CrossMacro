using System;
using System.Globalization;
using Avalonia.Data.Converters;
using CrossMacro.Core.Models;

namespace CrossMacro.UI.Views.Tabs;

/// <summary>
/// Converters for EditorActionType visibility in the editor UI.
/// </summary>
public static class ActionTypeConverters
{
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
