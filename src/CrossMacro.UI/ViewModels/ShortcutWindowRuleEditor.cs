namespace CrossMacro.UI.ViewModels;

/// <summary>Editable UI representation of one focused-window shortcut rule.</summary>
public sealed partial class ShortcutWindowRuleEditor : ObservableObject
{
    [ObservableProperty] private TriggerField field = TriggerField.WindowClass;
    [ObservableProperty] private TriggerMatchMode matchMode = TriggerMatchMode.Contains;
    [ObservableProperty] private string value = string.Empty;
    [ObservableProperty] private bool isRefreshingWindows;

    public ObservableCollection<string> AvailableWindowValues { get; } = [];

    public string? SelectedWindowValue
    {
        get => null;
        set
        {
            if (!string.IsNullOrEmpty(value))
            {
                Value = value;
            }

            OnPropertyChanged();
        }
    }

    public bool IsValid => new ShortcutWindowRule
    {
        Field = Field,
        MatchMode = MatchMode,
        Value = Value,
    }.IsValid();

    public void Load(ShortcutWindowRule source)
    {
        ArgumentNullException.ThrowIfNull(source);
        Field = source.Field;
        MatchMode = source.MatchMode;
        Value = source.Value;
    }

    public ShortcutWindowRule ToCore() => new()
    {
        Field = Field,
        MatchMode = MatchMode,
        Value = Value,
    };

    partial void OnFieldChanged(TriggerField value)
    {
        AvailableWindowValues.Clear();
        OnPropertyChanged(nameof(IsValid));
    }

    partial void OnMatchModeChanged(TriggerMatchMode value) => OnPropertyChanged(nameof(IsValid));

    partial void OnValueChanged(string value) => OnPropertyChanged(nameof(IsValid));
}
