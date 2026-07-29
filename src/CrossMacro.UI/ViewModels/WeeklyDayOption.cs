
namespace CrossMacro.UI.ViewModels;

public sealed class WeeklyDayOption(ScheduleViewModel owner, ScheduleDays value, string displayName) : ViewModelBase
{
    private readonly ScheduleViewModel _owner = owner;

    public ScheduleDays Value { get; } = value;

    public string DisplayName { get; } = displayName;

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
