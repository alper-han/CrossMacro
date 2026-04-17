using System;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CrossMacro.Core.Models;

namespace CrossMacro.UI.Models;

public sealed class LoadedMacroListItem : ObservableObject
{
    private const int MinimumSequenceRepeatCount = 1;
    private string _name;
    private int _sequenceRepeatCount = MinimumSequenceRepeatCount;

    public LoadedMacroListItem(MacroSequence macro, string? sourcePath = null, Guid? sessionId = null)
    {
        Macro = macro ?? throw new ArgumentNullException(nameof(macro));
        SourcePath = sourcePath;
        SessionId = sessionId ?? Guid.NewGuid();
        _name = string.IsNullOrWhiteSpace(macro.Name) ? MacroNameDefaults.UnnamedMacroName : macro.Name;
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
            var normalized = string.IsNullOrWhiteSpace(value) ? MacroNameDefaults.UnnamedMacroName : value.Trim();
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
        ? "Session"
        : Path.GetFileName(SourcePath) ?? SourcePath;

    public string SequenceRepeatSummary => $"Seq x{SequenceRepeatCount}";

    public string Description => $"{EventCount} events | {SourceDescription}";

    public LoadedMacroListItem CreateSnapshot()
    {
        var snapshot = new LoadedMacroListItem(Macro.Clone(), SourcePath, SessionId)
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

        var normalized = string.IsNullOrWhiteSpace(macro.Name) ? MacroNameDefaults.UnnamedMacroName : macro.Name.Trim();
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
}
