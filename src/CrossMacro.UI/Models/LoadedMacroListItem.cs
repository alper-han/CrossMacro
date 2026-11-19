
namespace CrossMacro.UI.Models;

public sealed class LoadedMacroListItem : ObservableObject
{
    private const int MinimumSequenceRepeatCount = 1;
    private string _name;
    private readonly ILocalizationService _localizationService;
    private bool _usesGeneratedName;

    public LoadedMacroListItem(MacroSequence macro, ILocalizationService localizationService, string? sourcePath = null, Guid? sessionId = null)
    {
        Macro = macro ?? throw new ArgumentNullException(nameof(macro));
        SourcePath = sourcePath;
        SessionId = sessionId ?? Guid.NewGuid();
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _usesGeneratedName = string.IsNullOrWhiteSpace(macro.Name);
        _name = _usesGeneratedName ? _localizationService["Files_UnnamedMacro"] : macro.Name;
        Macro.Name = _name;
    }

    public Guid SessionId { get; }

    public MacroSequence Macro { get; private set; }

    public string? SourcePath { get; private set; }

    public string Name
    {
        get => _name;
        set
        {
            _usesGeneratedName = string.IsNullOrWhiteSpace(value);
            var normalized = _usesGeneratedName ? _localizationService["Files_UnnamedMacro"] : value.Trim();
            if (string.Equals(_name, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _name = normalized;
            Macro.Name = normalized;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Description));
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public int SequenceRepeatCount
    {
        get;
        set
        {
            var normalized = Math.Max(MinimumSequenceRepeatCount, value);
            if (field == normalized)
            {
                return;
            }

            field = normalized;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SequenceRepeatSummary));
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    } = MinimumSequenceRepeatCount;

    internal event EventHandler? StateChanged;

    public int EventCount => MacroPlayableActionCounter.CountPlayableActions(Macro);

    public string SourceDescription => string.IsNullOrWhiteSpace(SourcePath)
        ? _localizationService["Files_SourceSession"]
        : Path.GetFileName(SourcePath) ?? SourcePath;

    public string SequenceRepeatSummary => string.Format(_localizationService.CurrentCulture, _localizationService["Files_SequenceRepeatSummary"], SequenceRepeatCount);

    public string Description => string.Format(_localizationService.CurrentCulture, _localizationService["Files_LoadedMacroDescription"], EventCount, SourceDescription);

    public LoadedMacroListItem CreateSnapshot()
    {
        return new LoadedMacroListItem(Macro.Clone(), _localizationService, SourcePath, SessionId)
        {
            SequenceRepeatCount = SequenceRepeatCount,
        };
    }

    public void UpdateSourcePath(string? sourcePath)
    {
        if (string.Equals(SourcePath, sourcePath, StringComparison.Ordinal))
        {
            return;
        }

        SourcePath = sourcePath;
        OnPropertyChanged(nameof(SourcePath));
        OnPropertyChanged(nameof(SourceDescription));
        OnPropertyChanged(nameof(Description));
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void UpdateMacro(MacroSequence macro, string? sourcePath = null)
    {
        ArgumentNullException.ThrowIfNull(macro);

        Macro = macro;
        if (sourcePath is not null)
        {
            UpdateSourcePath(sourcePath);
        }

        _usesGeneratedName = string.IsNullOrWhiteSpace(macro.Name);
        var normalized = _usesGeneratedName ? _localizationService["Files_UnnamedMacro"] : macro.Name.Trim();
        Macro.Name = normalized;

        var nameChanged = !string.Equals(_name, normalized, StringComparison.Ordinal);
        _name = normalized;

        if (nameChanged)
        {
            OnPropertyChanged(nameof(Name));
        }

        OnPropertyChanged(nameof(Macro));
        OnPropertyChanged(nameof(EventCount));
        OnPropertyChanged(nameof(Description));
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RefreshLocalizedProperties()
    {
        if (_usesGeneratedName)
        {
            var localizedName = _localizationService["Files_UnnamedMacro"];
            if (!string.Equals(_name, localizedName, StringComparison.Ordinal))
            {
                _name = localizedName;
                Macro.Name = localizedName;
                OnPropertyChanged(nameof(Name));
            }
        }

        OnPropertyChanged(nameof(SourceDescription));
        OnPropertyChanged(nameof(SequenceRepeatSummary));
        OnPropertyChanged(nameof(Description));
    }
}
