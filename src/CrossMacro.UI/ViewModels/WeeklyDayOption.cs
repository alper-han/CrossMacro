
namespace CrossMacro.UI.ViewModels;

public sealed class WeeklyDayOption : ViewModelBase
{
    private readonly ScheduleViewModel _owner;

    public WeeklyDayOption(ScheduleViewModel owner, ScheduleDays value, string displayName)
    {
        _owner = owner;
        Value = value;
        DisplayName = displayName;
    }

    public ScheduleDays Value { get; }

    public string DisplayName { get; }

    public bool IsSelected
    {
        get => _owner.HasWeeklyDay(Value);
        set => _owner.SetWeeklyDay(Value, value);
    }

    internal void RefreshSelection()
    {
        OnPropertyChanged(nameof(IsSelected));
    }
}
