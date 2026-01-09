using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CrossMacro.Core.Models;

/// <summary>
/// Represents a single action in the macro editor.
/// Provides a user-friendly abstraction over MacroEvent for editing.
/// Implements INotifyPropertyChanged for proper UI binding.
/// </summary>
public class EditorAction : INotifyPropertyChanged
{
    private Guid _id = Guid.NewGuid();
    private EditorActionType _type;
    private int _x;
    private int _y;
    private bool _isAbsolute = true;
    private MouseButton _button = MouseButton.Left;
    private int _keyCode;
    private int _delayMs;
    private int _scrollAmount = 1;
    private string? _keyName;
    private int _index;
    private string _text = string.Empty;
    
    public event PropertyChangedEventHandler? PropertyChanged;
    
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    
    /// <summary>
    /// Unique identifier for this action.
    /// </summary>
    public Guid Id
    {
        get => _id;
        set { _id = value; OnPropertyChanged(); }
    }
    
    /// <summary>
    /// Type of action to perform.
    /// </summary>
    public EditorActionType Type
    {
        get => _type;
        set 
        { 
            if (_type != value)
            {
                _type = value; 
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }
    
    /// <summary>
    /// X coordinate (for mouse actions).
    /// For absolute: screen position. For relative: offset.
    /// </summary>
    public int X
    {
        get => _x;
        set 
        { 
            if (_x != value)
            {
                _x = value; 
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }
    
    /// <summary>
    /// Y coordinate (for mouse actions).
    /// For absolute: screen position. For relative: offset.
    /// </summary>
    public int Y
    {
        get => _y;
        set 
        { 
            if (_y != value)
            {
                _y = value; 
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }
    
    /// <summary>
    /// Whether coordinates are absolute (true) or relative (false).
    /// Only applicable for MouseMove actions.
    /// </summary>
    public bool IsAbsolute
    {
        get => _isAbsolute;
        set 
        { 
            if (_isAbsolute != value)
            {
                _isAbsolute = value; 
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }
    
    /// <summary>
    /// Mouse button (for click/down/up actions).
    /// </summary>
    public MouseButton Button
    {
        get => _button;
        set 
        { 
            if (_button != value)
            {
                _button = value; 
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }
    
    /// <summary>
    /// Keyboard key code (for key actions).
    /// Uses Linux input key codes.
    /// </summary>
    public int KeyCode
    {
        get => _keyCode;
        set 
        { 
            if (_keyCode != value)
            {
                _keyCode = value; 
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }
    
    /// <summary>
    /// Delay in milliseconds (for Delay action or timing between actions).
    /// </summary>
    public int DelayMs
    {
        get => _delayMs;
        set 
        { 
            if (_delayMs != value)
            {
                _delayMs = value; 
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }
    
    /// <summary>
    /// Scroll amount (positive = up/right, negative = down/left).
    /// </summary>
    public int ScrollAmount
    {
        get => _scrollAmount;
        set 
        { 
            if (_scrollAmount != value)
            {
                _scrollAmount = value; 
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }
    
    /// <summary>
    /// Human-readable key name for display purposes.
    /// </summary>
    public string? KeyName
    {
        get => _keyName;
        set 
        { 
            if (_keyName != value)
            {
                _keyName = value; 
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }
    
    /// <summary>
    /// Index of this action in the list (1-based for display).
    /// </summary>
    public int Index
    {
        get => _index;
        set 
        { 
            if (_index != value)
            {
                _index = value; 
                OnPropertyChanged();
            }
        }
    }
    
    /// <summary>
    /// Text content (for TextInput action).
    /// Each character will be converted to a KeyPress event when saving.
    /// </summary>
    public string Text
    {
        get => _text;
        set 
        { 
            if (_text != value)
            {
                _text = value ?? string.Empty; 
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }
    
    /// <summary>
    /// Gets a human-readable description of this action.
    /// </summary>
    public string DisplayName => GenerateDisplayName();
    
    private string GenerateDisplayName()
    {
        return Type switch
        {
            EditorActionType.MouseMove when IsAbsolute => $"Move to ({X}, {Y})",
            EditorActionType.MouseMove => $"Move by ({X:+#;-#;0}, {Y:+#;-#;0})",
            EditorActionType.MouseClick => $"Click {Button}",
            EditorActionType.MouseDown => $"Hold {Button}",
            EditorActionType.MouseUp => $"Release {Button}",
            EditorActionType.KeyPress => $"Press '{KeyName ?? KeyCode.ToString()}'",
            EditorActionType.KeyDown => $"Hold '{KeyName ?? KeyCode.ToString()}'",
            EditorActionType.KeyUp => $"Release '{KeyName ?? KeyCode.ToString()}'",
            EditorActionType.Delay => $"Wait {DelayMs}ms",
            EditorActionType.ScrollVertical => ScrollAmount > 0 ? $"Scroll Up {ScrollAmount}" : $"Scroll Down {Math.Abs(ScrollAmount)}",
            EditorActionType.ScrollHorizontal => ScrollAmount > 0 ? $"Scroll Right {ScrollAmount}" : $"Scroll Left {Math.Abs(ScrollAmount)}",
            EditorActionType.TextInput => string.IsNullOrEmpty(Text) 
                ? "Text Input (empty)" 
                : $"Type \"{(Text.Length > 25 ? Text[..25] + "..." : Text)}\"",
            _ => "Unknown Action"
        };
    }
    
    /// <summary>
    /// Validates this action.
    /// </summary>
    /// <returns>True if valid, false otherwise.</returns>
    public bool IsValid()
    {
        return Type switch
        {
            EditorActionType.Delay => DelayMs >= 0,
            EditorActionType.KeyPress or EditorActionType.KeyDown or EditorActionType.KeyUp => KeyCode > 0,
            EditorActionType.ScrollVertical or EditorActionType.ScrollHorizontal => ScrollAmount != 0,
            EditorActionType.TextInput => !string.IsNullOrEmpty(Text),
            _ => true
        };
    }
    
    public EditorAction Clone()
    {
        return new EditorAction
        {
            _id = Guid.NewGuid(), // New ID for clone
            _type = Type,
            _x = X,
            _y = Y,
            _isAbsolute = IsAbsolute,
            _button = Button,
            _keyCode = KeyCode,
            _delayMs = DelayMs,
            _scrollAmount = ScrollAmount,
            _keyName = KeyName,
            _text = Text
        };
    }
}
