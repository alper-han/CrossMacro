namespace CrossMacro.UI.ViewModels.Editor;

/// <summary>The grouped action-type picker used to choose what "Add action" inserts.</summary>
public sealed class EditorNewActionPicker : ObservableObject
{
    private static readonly IReadOnlyList<(string ResourceKey, EditorActionType[] ActionTypes)> GroupDefinitions =
        [
            ("Editor_ActionGroup_Mouse", new[]
            {
                EditorActionType.MouseMove,
                EditorActionType.MouseClick,
                EditorActionType.MouseDown,
                EditorActionType.MouseUp,
                EditorActionType.MousePosition,
                EditorActionType.ScrollVertical,
                EditorActionType.ScrollHorizontal,
            }),
            ("Editor_ActionGroup_Keyboard", new[]
            {
                EditorActionType.KeyPress,
                EditorActionType.KeyDown,
                EditorActionType.KeyUp,
            }),
            ("Editor_ActionGroup_Timing", new[] { EditorActionType.Delay }),
            ("Editor_ActionGroup_Text", new[] { EditorActionType.TextInput }),
            ("Editor_ActionGroup_Variables", new[]
            {
                EditorActionType.SetVariable,
                EditorActionType.CopySelectionToVariable,
                EditorActionType.IncrementVariable,
                EditorActionType.DecrementVariable,
                EditorActionType.MultiplyVariable,
                EditorActionType.DivideVariable,
            }),
            ("Editor_ActionGroup_FlowControl", new[]
            {
                EditorActionType.RepeatBlockStart,
                EditorActionType.IfBlockStart,
                EditorActionType.WhileBlockStart,
                EditorActionType.ForBlockStart,
                EditorActionType.Break,
                EditorActionType.Continue,
            }),
            ("Editor_ActionGroup_ScreenReading", new[]
            {
                EditorActionType.PixelColor,
                EditorActionType.WaitColor,
                EditorActionType.PixelSearch,
                EditorActionType.ImageSearch,
                EditorActionType.ImageClick,
                EditorActionType.WaitImage,
            }),
            ("Editor_ActionGroup_Screenshot", new[]
            {
                EditorActionType.Screenshot,
            }),
            ("Editor_ActionGroup_Clipboard", new[]
            {
                EditorActionType.ClipboardGet,
                EditorActionType.ClipboardSet,
            }),
            ("Editor_ActionGroup_Shell", new[]
            {
                EditorActionType.ShellCommand,
            }),
            ("Editor_ActionGroup_Window", new[]
            {
                EditorActionType.WindowCommand,
            }),
        ];

    private readonly EditorViewModel _owner;
    private readonly EditorActionDisplayFormatter _formatter;
    private EditorActionPickerGroup? _group;
    private EditorActionPickerChoice? _choice;

    internal EditorNewActionPicker(EditorViewModel owner, EditorActionDisplayFormatter formatter)
    {
        _owner = owner;
        _formatter = formatter;
        Rebuild(ActionType);
    }

    public EditorActionType ActionType
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                SyncSelection(value);
            }
        }
    } = EditorActionType.MouseClick;

    public IReadOnlyList<EditorActionPickerGroup> Groups { get; private set; } = [];

    // Kept manual: reference-equality guard with cascading picker synchronization that writes the backing fields directly.
    public EditorActionPickerGroup? Group
    {
        get => _group;
        set
        {
            if (ReferenceEquals(_group, value))
            {
                return;
            }

            _group = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Choices));

            if (value != null && !value.Choices.Contains(_choice))
            {
                Choice = value.Choices.Count > 0 ? value.Choices[0] : null;
            }
        }
    }

    public IReadOnlyList<EditorActionPickerChoice> Choices => Group?.Choices ?? [];

    // Kept manual: reference-equality guard with cascading picker synchronization that writes the backing fields directly.
    public EditorActionPickerChoice? Choice
    {
        get => _choice;
        set
        {
            if (ReferenceEquals(_choice, value))
            {
                return;
            }

            _choice = value;
            OnPropertyChanged();

            if (value != null && ActionType != value.ActionType)
            {
                ActionType = value.ActionType;
            }
        }
    }

    /// <summary>Rebuilds the localized groups (for example after a culture change) and keeps the preferred type selected.</summary>
    internal void Rebuild(EditorActionType preferredActionType)
    {
        Groups = GroupDefinitions
            .Select(definition => new EditorActionPickerGroup(
                _owner.Localize(definition.ResourceKey),
                definition.ActionTypes
                    .Where(EditorViewModel.IsUserAddableActionType)
                    .Select(actionType => new EditorActionPickerChoice(actionType, _formatter.FormatActionType(actionType)))
                    .ToArray()))
            .Where(group => group.Choices.Count > 0)
            .ToArray();

        OnPropertyChanged(nameof(Groups));
        SyncSelection(preferredActionType);
    }

    private void SyncSelection(EditorActionType actionType)
    {
        foreach (var group in Groups)
        {
            var choice = group.Choices.FirstOrDefault(item => item.ActionType == actionType);
            if (choice == null)
            {
                continue;
            }

            var groupChanged = !ReferenceEquals(_group, group);
            var choiceChanged = !ReferenceEquals(_choice, choice);
            _group = group;
            _choice = choice;

            if (groupChanged)
            {
                OnPropertyChanged(nameof(Group));
                OnPropertyChanged(nameof(Choices));
            }

            if (choiceChanged)
            {
                OnPropertyChanged(nameof(Choice));
            }

            return;
        }

        var fallbackGroup = Groups.Count > 0 ? Groups[0] : null;
        _group = fallbackGroup;
        _choice = fallbackGroup?.Choices.Count > 0 ? fallbackGroup.Choices[0] : null;

        OnPropertyChanged(nameof(Group));
        OnPropertyChanged(nameof(Choices));
        OnPropertyChanged(nameof(Choice));
    }
}
