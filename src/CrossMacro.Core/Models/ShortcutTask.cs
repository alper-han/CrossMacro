using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CrossMacro.Core.Models;

/// <summary>
/// Represents a shortcut-triggered macro task
/// </summary>
public class ShortcutTask : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Unique identifier for this shortcut task
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// Display name for the task
    /// </summary>
    private string _name = "New Shortcut";
    public string Name 
    { 
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }
    
    /// <summary>
    /// Path to the macro file to execute
    /// </summary>
    private string _macroFilePath = string.Empty;
    public string MacroFilePath 
    { 
        get => _macroFilePath;
        set 
        { 
            _macroFilePath = value; 
            OnPropertyChanged(); 
            OnPropertyChanged(nameof(CanBeEnabled));
        }
    }
    
    /// <summary>
    /// Hotkey string (e.g., "Ctrl+Shift+P")
    /// </summary>
    private string _hotkeyString = string.Empty;
    public string HotkeyString 
    { 
        get => _hotkeyString;
        set 
        { 
            _hotkeyString = value; 
            OnPropertyChanged(); 
            OnPropertyChanged(nameof(CanBeEnabled));
        }
    }
    
    /// <summary>
    /// Playback speed multiplier (0.1 = 10x slower, 1.0 = normal, 10.0 = 10x faster)
    /// </summary>
    private double _playbackSpeed = 1.0;
    public double PlaybackSpeed 
    { 
        get => _playbackSpeed;
        set { _playbackSpeed = value; OnPropertyChanged(); }
    }
    
    /// <summary>
    /// Whether the shortcut is enabled
    /// </summary>
    private bool _isEnabled;
    public bool IsEnabled 
    { 
        get => _isEnabled;
        set
        {
            // Can only enable if macro file and hotkey are set
            if (value && !CanBeEnabled)
            {
                return;
            }
            
            _isEnabled = value;
            OnPropertyChanged();
        }
    }
    
    /// <summary>
    /// Whether the task can be enabled (has both macro file path and hotkey)
    /// </summary>
    public bool CanBeEnabled => !string.IsNullOrEmpty(MacroFilePath) && !string.IsNullOrEmpty(HotkeyString);

    private bool _loopEnabled;
    public bool LoopEnabled
    {
        get => _loopEnabled;
        set
        {
            if (_loopEnabled == value) return;
            _loopEnabled = value;
            if (value) { _runWhileHeld = false; OnPropertyChanged(nameof(RunWhileHeld)); }
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsLoopEnabled));
        }
    }

    private int _repeatCount = 0;
    public int RepeatCount
    {
        get => _repeatCount;
        set { _repeatCount = value; OnPropertyChanged(); }
    }

    private int _repeatDelayMs = 0;
    public int RepeatDelayMs
    {
        get => _repeatDelayMs;
        set { _repeatDelayMs = value; OnPropertyChanged(); }
    }

    private bool _runWhileHeld;
    public bool RunWhileHeld
    {
        get => _runWhileHeld;
        set
        {
            if (_runWhileHeld == value) return;
            _runWhileHeld = value;
            if (value) { _loopEnabled = false; OnPropertyChanged(nameof(LoopEnabled)); }
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsLoopEnabled));
        }
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsLoopEnabled
    {
        get => _loopEnabled || _runWhileHeld;
        set
        {
            if (value == IsLoopEnabled) return;
            if (value) { _loopEnabled = true; _runWhileHeld = false; OnPropertyChanged(nameof(LoopEnabled)); OnPropertyChanged(nameof(RunWhileHeld)); }
            else { _loopEnabled = false; _runWhileHeld = false; OnPropertyChanged(nameof(LoopEnabled)); OnPropertyChanged(nameof(RunWhileHeld)); }
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Status message from last execution
    /// </summary>
    private string? _lastStatus;
    public string? LastStatus 
    { 
        get => _lastStatus;
        set { _lastStatus = value; OnPropertyChanged(); }
    }
    
    /// <summary>
    /// When the shortcut was last triggered
    /// </summary>
    private DateTime? _lastTriggeredTime;
    public DateTime? LastTriggeredTime 
    { 
        get => _lastTriggeredTime;
        set { _lastTriggeredTime = value; OnPropertyChanged(); }
    }
}
