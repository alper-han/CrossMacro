
namespace CrossMacro.Editing.Models;

/// <summary>
/// Represents a single action in the macro editor.
/// Provides a user-friendly abstraction over MacroEvent for editing.
/// Implements INotifyPropertyChanged for proper UI binding.
/// </summary>
public partial class EditorAction : INotifyPropertyChanged
{
    private Guid _id;
    private EditorActionType _type;
    private readonly InputState _input;
    private readonly ScriptState _script;
    private readonly ScreenState _screen;
    private readonly ShellState _shell;
    private readonly ScreenshotState _screenshot;
    private readonly WindowState _window;
    private string _comment;

    public EditorAction()
    {
        _id = Guid.NewGuid();
        _input = new();
        _script = new();
        _screen = new();
        _shell = new();
        _screenshot = new();
        _window = new();
        _comment = string.Empty;
    }

    private EditorAction(EditorAction source)
    {
        _id = Guid.NewGuid();
        _type = source._type;
        _input = source._input.Copy();
        _script = source._script.Copy();
        _screen = source._screen.Copy();
        _shell = source._shell.Copy();
        _screenshot = source._screenshot.Copy();
        _window = source._window.Copy();
        _comment = source._comment;
    }

    /// <summary>
    /// Optional editor-only note associated with this action.
    /// </summary>
    public string Comment
    {
        get => _comment;
        set => SetField(ref _comment, value ?? string.Empty);
    }

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
        set => SetField(ref _id, value);
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
                if (value is not EditorActionType.TextInput)
                {
                    ClearPreservedTextInputEvents();
                }

                if (!EditorActionScriptClassifier.IsScriptPayloadAction(value))
                {
                    PreferLegacyScriptText = false;
                }
                else if (!string.IsNullOrWhiteSpace(_input.Text))
                {
                    PreferLegacyScriptText = true;
                }
                OnPropertyChanged();
            }
        }
    }

    public EditorAction Clone() => new(this);

    private void MarkStructuredScriptEdited()
    {
        if (EditorActionScriptClassifier.IsScriptPayloadAction(Type))
        {
            PreferLegacyScriptText = false;
        }
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
    }

    private void SetScriptField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        MarkStructuredScriptEdited();
        OnPropertyChanged(propertyName);
    }
}
