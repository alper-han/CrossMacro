namespace CrossMacro.UI.ViewModels.Editor;

/// <summary>The title and inline rename state shown on the editor's tab header.</summary>
public sealed class EditorTabState : ObservableObject
{
    private readonly EditorViewModel _owner;
    private string _untitledTitle;

    internal EditorTabState(EditorViewModel owner, string untitledTitle)
    {
        _owner = owner;
        _untitledTitle = untitledTitle;
    }

    public string Title
    {
        get
        {
            if (!UsesDefaultMacroName && !string.IsNullOrWhiteSpace(_owner.MacroName))
            {
                return _owner.MacroName.Trim();
            }

            return string.IsNullOrWhiteSpace(_owner.SourcePath)
                ? _untitledTitle
                : Path.GetFileNameWithoutExtension(_owner.SourcePath);
        }
    }

    public string DisplayTitle => _owner.IsDirty ? $"{Title} *" : Title;

    public bool IsRenaming { get; private set => SetProperty(ref field, value); }

    public string RenameText
    {
        get;
        set => SetProperty(ref field, value);
    } = string.Empty;

    internal bool UsesDefaultMacroName { get; private set; } = true;

    public void SetUntitledTitle(string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        if (string.Equals(_untitledTitle, title, StringComparison.Ordinal))
        {
            return;
        }

        _untitledTitle = title;
        NotifyTitleChanged();
    }

    public void BeginRename()
    {
        RenameText = Title;
        IsRenaming = true;
    }

    public void CommitRename()
    {
        if (!IsRenaming)
        {
            return;
        }

        IsRenaming = false;
        if (string.IsNullOrWhiteSpace(RenameText))
        {
            return;
        }

        _owner.MacroName = RenameText.Trim();
        UsesDefaultMacroName = false;
        NotifyTitleChanged();
    }

    public void CancelRename() => IsRenaming = false;

    /// <summary>Tracks whether the macro name is still the localized default after the name changed.</summary>
    internal void OnMacroNameChanged(string name)
    {
        UsesDefaultMacroName = string.IsNullOrWhiteSpace(name)
            || string.Equals(name.Trim(), _owner.Localize("Editor_DefaultMacroName"), StringComparison.Ordinal);
        NotifyTitleChanged();
    }

    internal void NotifyTitleChanged()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(DisplayTitle));
    }
}
