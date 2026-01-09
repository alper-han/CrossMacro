using System.Collections.Generic;
using CrossMacro.Core.Models;
using CrossMacro.Core.Resources;
using CrossMacro.Core.Services;

namespace CrossMacro.Infrastructure.Services;

/// <summary>
/// Validates EditorAction instances with comprehensive rule checking.
/// </summary>
public class EditorActionValidator : IEditorActionValidator
{
    /// <summary>
    /// Maximum allowed length for TextInput content.
    /// </summary>
    public const int MaxTextInputLength = 500;
    
    /// <inheritdoc/>
    public (bool IsValid, string? Error) Validate(EditorAction action)
    {
        if (action == null)
            return (false, ValidationMessages.ActionCannotBeNull);
        
        return action.Type switch
        {
            EditorActionType.Delay => ValidateDelay(action),
            EditorActionType.KeyPress or EditorActionType.KeyDown or EditorActionType.KeyUp => ValidateKeyAction(action),
            EditorActionType.ScrollVertical or EditorActionType.ScrollHorizontal => ValidateScroll(action),
            EditorActionType.MouseMove => ValidateMouseMove(action),
            EditorActionType.MouseClick or EditorActionType.MouseDown or EditorActionType.MouseUp => ValidateMouseButton(action),
            EditorActionType.TextInput => ValidateTextInput(action),
            _ => (true, null)
        };
    }
    
    /// <inheritdoc/>
    public (bool IsValid, List<string> Errors) ValidateAll(IEnumerable<EditorAction> actions)
    {
        var errors = new List<string>();
        int index = 0;
        bool? firstCoordinateMode = null;
        
        foreach (var action in actions)
        {
            // Validate individual action
            var (isValid, error) = Validate(action);
            if (!isValid && error != null)
            {
                errors.Add($"Action {index + 1} ({action.Type}): {error}");
            }
            
            // Validate coordinate mode consistency
            if (action.Type == EditorActionType.MouseMove)
            {
                if (firstCoordinateMode == null)
                {
                    firstCoordinateMode = action.IsAbsolute;
                }
                else if (firstCoordinateMode != action.IsAbsolute)
                {
                    errors.Add($"Action {index + 1}: Cannot mix Absolute and Relative coordinates in the same macro.");
                }
            }
            
            index++;
        }
        
        return (errors.Count == 0, errors);
    }
    
    private static (bool IsValid, string? Error) ValidateDelay(EditorAction action)
    {
        if (action.DelayMs < 0)
            return (false, ValidationMessages.DelayMustBeNonNegative);
        
        if (action.DelayMs == 0)
            return (false, ValidationMessages.DelayMustBePositive);
        
        if (action.DelayMs > 3600000) // 1 hour max
            return (false, ValidationMessages.DelayTooLong);
        
        return (true, null);
    }
    
    private static (bool IsValid, string? Error) ValidateKeyAction(EditorAction action)
    {
        if (action.KeyCode <= 0)
            return (false, ValidationMessages.KeyCodeMustBePositive);
        
        if (action.KeyCode > 767) // Max Linux key code
            return (false, ValidationMessages.KeyCodeInvalid);
        
        return (true, null);
    }
    
    private static (bool IsValid, string? Error) ValidateScroll(EditorAction action)
    {
        if (action.ScrollAmount == 0)
            return (false, ValidationMessages.ScrollAmountCannotBeZero);
        
        if (Math.Abs(action.ScrollAmount) > 100)
            return (false, ValidationMessages.ScrollAmountTooLarge);
        
        return (true, null);
    }
    
    private static (bool IsValid, string? Error) ValidateMouseMove(EditorAction action)
    {
        if (action.IsAbsolute)
        {
            if (action.X < 0 || action.Y < 0)
                return (false, ValidationMessages.AbsoluteCoordsMustBeNonNegative);
            
            if (action.X > 32767 || action.Y > 32767)
                return (false, ValidationMessages.CoordsExceedMaximum);
        }
        else
        {
            if (action.X == 0 && action.Y == 0)
                return (false, ValidationMessages.RelativeMoveMustHaveValue);
            
            if (Math.Abs(action.X) > 10000 || Math.Abs(action.Y) > 10000)
                return (false, ValidationMessages.RelativeMoveTooLarge);
        }
        
        return (true, null);
    }
    
    private static (bool IsValid, string? Error) ValidateMouseButton(EditorAction action)
    {
        if (!Enum.IsDefined(typeof(MouseButton), action.Button))
            return (false, ValidationMessages.InvalidMouseButton);
        
        if (action.Button is MouseButton.ScrollUp or MouseButton.ScrollDown 
            or MouseButton.ScrollLeft or MouseButton.ScrollRight)
            return (false, ValidationMessages.UseScrollActionForScrollButtons);
        
        return (true, null);
    }
    
    private static (bool IsValid, string? Error) ValidateTextInput(EditorAction action)
    {
        if (string.IsNullOrEmpty(action.Text))
            return (false, "Text content is required for TextInput action");
        
        if (action.Text.Length > MaxTextInputLength)
            return (false, $"Text content exceeds maximum length of {MaxTextInputLength} characters");
        
        return (true, null);
    }
}

