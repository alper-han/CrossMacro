
namespace CrossMacro.Core.Models;

/// <summary>
/// A window-state trigger: when the active window matches the configured
/// condition, the configured action (e.g. switch profile) runs.
/// </summary>
public class TriggerTask : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public Guid Id { get; set; } = Guid.NewGuid();

    private string _name = "New Trigger";
    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    private TriggerField _field = TriggerField.WindowClass;
    public TriggerField Field
    {
        get => _field;
        set { _field = value; OnPropertyChanged(); }
    }

    private TriggerMatchMode _matchMode = TriggerMatchMode.Contains;
    public TriggerMatchMode MatchMode
    {
        get => _matchMode;
        set { _matchMode = value; OnPropertyChanged(); }
    }

    private string _value = string.Empty;
    public string Value
    {
        get => _value;
        set
        {
            _value = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanBeEnabled));
        }
    }

    private TriggerAction _action = TriggerAction.SwitchProfile;
    public TriggerAction Action
    {
        get => _action;
        set { _action = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Target profile id for <see cref="TriggerAction.SwitchProfile"/>.
    /// Matches <see cref="ProfileInfo.Id"/> (a stable slug string).
    /// </summary>
    private string _targetProfileId = string.Empty;
    public string TargetProfileId
    {
        get => _targetProfileId;
        set
        {
            _targetProfileId = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanBeEnabled));
        }
    }

    /// <summary>
    /// Macro file path for <see cref="TriggerAction.RunMacro"/>.
    /// Matches <see cref="ShortcutTask.MacroFilePath"/> / <see cref="ScheduledTask.MacroFilePath"/>.
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

    private TriggerFireMode _fireMode = TriggerFireMode.OnceOnChange;
    public TriggerFireMode FireMode
    {
        get => _fireMode;
        set { _fireMode = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Minimum milliseconds between consecutive fires.
    /// 0 or null = no cooldown.
    /// </summary>
    private int? _cooldownMs;
    public int? CooldownMs
    {
        get => _cooldownMs;
        set { _cooldownMs = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// How long the match must stay true before the trigger fires.
    /// 0 or null = no debounce.
    /// </summary>
    private int? _debounceMs;
    public int? DebounceMs
    {
        get => _debounceMs;
        set { _debounceMs = value; OnPropertyChanged(); }
    }

    private bool _isEnabled;
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (value && !CanBeEnabled) return;
            _isEnabled = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// A trigger can be enabled only when it has a non-empty value and,
    /// depending on the action, a non-empty target profile id or macro file path.
    /// <see cref="TriggerField.None"/> needs no value (pure interval path).
    /// </summary>
    public bool CanBeEnabled =>
        (Field is TriggerField.None || !string.IsNullOrEmpty(Value))
&& (Action is not TriggerAction.SwitchProfile || !string.IsNullOrEmpty(TargetProfileId))
&& (Action is not TriggerAction.RunMacro || !string.IsNullOrEmpty(MacroFilePath));

    private DateTime? _lastTriggeredTime;
    public DateTime? LastTriggeredTime
    {
        get => _lastTriggeredTime;
        set { _lastTriggeredTime = value; OnPropertyChanged(); }
    }

    private string? _lastStatus;
    public string? LastStatus
    {
        get => _lastStatus;
        set { _lastStatus = value; OnPropertyChanged(); }
    }
}
