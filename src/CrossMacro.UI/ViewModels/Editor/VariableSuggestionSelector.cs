namespace CrossMacro.UI.ViewModels.Editor;

/// <summary>
/// One variable-name picker: choosing an entry applies it to the selected action and then resets the
/// picker, so it always offers the list again instead of showing the last pick.
/// </summary>
public sealed class VariableSuggestionSelector : ObservableObject
{
    private readonly VariableSuggestionScope _scope;
    private readonly Action<string> _apply;
    private readonly Func<string?, string?>? _project;
    private string? _selected;

    /// <param name="scope">Shared re-entrancy guard of every picker of one editor.</param>
    /// <param name="apply">Writes the chosen variable name into the model.</param>
    /// <param name="project">Optionally projects the stored pick onto the value the picker should display.</param>
    internal VariableSuggestionSelector(VariableSuggestionScope scope, Action<string> apply, Func<string?, string?>? project = null)
    {
        _scope = scope;
        _apply = apply;
        _project = project;
    }

    public string? Selected
    {
        get => _project is null ? _selected : _project(_selected);
        set
        {
            if (string.Equals(_selected, value, StringComparison.Ordinal))
            {
                return;
            }

            _selected = value;
            OnPropertyChanged();

            if (_scope.IsApplying || string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            _scope.IsApplying = true;
            try
            {
                _apply(value);
                _selected = null;
                OnPropertyChanged();
            }
            finally
            {
                _scope.IsApplying = false;
            }
        }
    }

    internal void Clear()
    {
        if (_selected is null)
        {
            return;
        }

        _selected = null;
        OnPropertyChanged(nameof(Selected));
    }

    internal void NotifyChanged() => OnPropertyChanged(nameof(Selected));
}
