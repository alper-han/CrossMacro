using System;
using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.UI.Localization;

namespace CrossMacro.UI.Models;

public sealed class LoadedMacroListItem : ObservableObject
{
    private const int MinimumSequenceRepeatCount = 1;
    private string _name;
    private int _sequenceRepeatCount = MinimumSequenceRepeatCount;
    private readonly ILocalizationService? _localizationService;
    private bool _usesGeneratedName;

    public LoadedMacroListItem(MacroSequence macro, string? sourcePath = null, Guid? sessionId = null, ILocalizationService? localizationService = null)
    {
        Macro = macro ?? throw new ArgumentNullException(nameof(macro));
        SourcePath = sourcePath;
        SessionId = sessionId ?? Guid.NewGuid();
        _localizationService = localizationService;
        _usesGeneratedName = string.IsNullOrWhiteSpace(macro.Name);
        _name = _usesGeneratedName ? GetString("Files_UnnamedMacro", MacroNameDefaults.NewRecordedMacroName) : macro.Name;
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
            var normalized = _usesGeneratedName ? GetString("Files_UnnamedMacro", MacroNameDefaults.NewRecordedMacroName) : value.Trim();
            if (_name == normalized)
            {
                return;
            }

            _name = normalized;
            Macro.Name = normalized;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Description));
        }
    }

    public int SequenceRepeatCount
    {
        get => _sequenceRepeatCount;
        set
        {
            var normalized = Math.Max(MinimumSequenceRepeatCount, value);
            if (_sequenceRepeatCount == normalized)
            {
                return;
            }

            _sequenceRepeatCount = normalized;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SequenceRepeatSummary));
        }
    }

    public int EventCount => Macro.Events?.Count ?? 0;

    public string SourceDescription => string.IsNullOrWhiteSpace(SourcePath)
        ? GetString("Files_SourceSession", "Session")
        : Path.GetFileName(SourcePath) ?? SourcePath;

    public string SequenceRepeatSummary => string.Format(GetCulture(), GetString("Files_SequenceRepeatSummary", "Seq x{0}"), SequenceRepeatCount);

    public string Description => string.Format(GetCulture(), GetString("Files_LoadedMacroDescription", "{0} events | {1}"), EventCount, SourceDescription);

    public LoadedMacroListItem CreateSnapshot()
    {
        var snapshot = new LoadedMacroListItem(Macro.Clone(), SourcePath, SessionId, _localizationService)
        {
            SequenceRepeatCount = SequenceRepeatCount
        };

        return snapshot;
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
    }

    public void UpdateMacro(MacroSequence macro, string? sourcePath = null)
    {
        ArgumentNullException.ThrowIfNull(macro);

        Macro = macro;
        if (sourcePath != null)
        {
            UpdateSourcePath(sourcePath);
        }

        _usesGeneratedName = string.IsNullOrWhiteSpace(macro.Name);
        var normalized = _usesGeneratedName ? GetString("Files_UnnamedMacro", MacroNameDefaults.NewRecordedMacroName) : macro.Name.Trim();
        Macro.Name = normalized;

        var nameChanged = _name != normalized;
        _name = normalized;

        if (nameChanged)
        {
            OnPropertyChanged(nameof(Name));
        }

        OnPropertyChanged(nameof(Macro));
        OnPropertyChanged(nameof(EventCount));
        OnPropertyChanged(nameof(Description));
    }

    public void RefreshLocalizedProperties()
    {
        if (_usesGeneratedName)
        {
            var localizedName = GetString("Files_UnnamedMacro", MacroNameDefaults.NewRecordedMacroName);
            if (_name != localizedName)
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

    private string GetString(string key, string fallback)
    {
        return _localizationService?[key] ?? Resources.ResourceManager.GetString(key, Resources.Culture) ?? fallback;
    }

    private CultureInfo GetCulture()
    {
        return _localizationService?.CurrentCulture ?? Resources.Culture ?? CultureInfo.CurrentUICulture;
    }
}
