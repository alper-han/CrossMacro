using System.Collections.ObjectModel;
using System.Linq;
using CrossMacro.Core.Models;
using CrossMacro.UI.Models;

namespace CrossMacro.UI.ViewModels;

public sealed class DesignRecordingViewModel : RecordingViewModel
{
    public DesignRecordingViewModel() : this(new DesignPreviewContext())
    {
    }

    internal DesignRecordingViewModel(DesignPreviewContext context)
        : base(context.MacroRecorder, context.HotkeyService, context.SettingsService, context.LocalizationService)
    {
        SetMacro(DesignPreviewSamples.CreateMacro("Invoice Form Fill"));
    }
}

public sealed class DesignPlaybackViewModel : PlaybackViewModel
{
    public DesignPlaybackViewModel() : this(new DesignPreviewContext())
    {
    }

    internal DesignPlaybackViewModel(DesignPreviewContext context)
        : base(context.MacroPlayer, context.SettingsService, context.LoadedMacroSession, context.LocalizationService)
    {
        context.LoadedMacroSession.PlaybackMode = LoadedMacroPlaybackMode.SequentialCycle;

        var first = context.LoadedMacroSession.AddMacro(DesignPreviewSamples.CreateMacro("Refresh Dashboard Loop"));
        first.SequenceRepeatCount = 4;

        var second = context.LoadedMacroSession.AddMacro(DesignPreviewSamples.CreateMacro("Retry Failed Uploads"));
        second.SequenceRepeatCount = 2;
    }
}

public sealed class DesignFilesViewModel : FilesViewModel
{
    public DesignFilesViewModel() : this(new DesignPreviewContext())
    {
    }

    internal DesignFilesViewModel(DesignPreviewContext context)
        : base(context.MacroFileManager, context.DialogService, context.LoadedMacroSession, context.LocalizationService)
    {
        context.LoadedMacroSession.PlaybackMode = LoadedMacroPlaybackMode.SequentialCycle;

        var first = context.LoadedMacroSession.AddMacro(
            DesignPreviewSamples.CreateMacro("Nightly Export Retry"),
            "/tmp/nightly-export-retry.macro");
        first.SequenceRepeatCount = 3;

        var second = context.LoadedMacroSession.AddMacro(
            DesignPreviewSamples.CreateMacro("Refresh Dashboard Loop"),
            "/tmp/refresh-dashboard-loop.macro");
        second.SequenceRepeatCount = 2;
    }
}

public sealed class DesignTextExpansionViewModel : TextExpansionViewModel
{
    public DesignTextExpansionViewModel() : this(new DesignPreviewContext())
    {
    }

    internal DesignTextExpansionViewModel(DesignPreviewContext context)
        : base(context.TextExpansionStorageService, context.DialogService, context.EnvironmentInfoProvider, context.LocalizationService)
    {
        TriggerInput = ":sync-ok";
        ReplacementInput = "Inventory sync completed successfully";
        SelectedInsertionMode = TextInsertionMode.Paste;
        SelectedPasteMethod = PasteMethod.CtrlShiftV;
        Expansions = new ObservableCollection<TextExpansion>(DesignPreviewSamples.CreateTextExpansions());
        OnPropertyChanged(nameof(HasExpansions));
    }
}

public sealed class DesignSettingsViewModel : SettingsViewModel
{
    public DesignSettingsViewModel() : this(new DesignPreviewContext())
    {
    }

    internal DesignSettingsViewModel(DesignPreviewContext context)
        : base(
            context.HotkeyService,
            context.SettingsService,
            context.TextExpansionService,
            context.HotkeySettings,
            context.ExternalUrlOpener,
            context.ThemeService,
            context.LocalizationService,
            context.RuntimeContext)
    {
    }
}

public sealed class DesignScheduleViewModel : ScheduleViewModel
{
    public DesignScheduleViewModel() : this(new DesignPreviewContext())
    {
    }

    internal DesignScheduleViewModel(DesignPreviewContext context)
        : base(context.SchedulerService, context.DialogService, context.TimeProvider, context.LocalizationService)
    {
        SelectedTask = Tasks.FirstOrDefault();
    }
}

public sealed class DesignShortcutViewModel : ShortcutViewModel
{
    public DesignShortcutViewModel() : this(new DesignPreviewContext())
    {
    }

    internal DesignShortcutViewModel(DesignPreviewContext context)
        : base(context.ShortcutService, context.DialogService, context.LocalizationService)
    {
        SelectedTask = Tasks.FirstOrDefault();
    }
}

public sealed class DesignEditorViewModel : EditorViewModel
{
    public DesignEditorViewModel() : this(new DesignPreviewContext())
    {
    }

    internal DesignEditorViewModel(DesignPreviewContext context)
        : base(
            context.EditorActionConverter,
            context.EditorActionValidator,
            context.CoordinateCaptureService,
            context.MacroFileManager,
            context.DialogService,
            context.KeyCodeMapper,
            context.LocalizationService,
            new CrossMacro.UI.Localization.EditorActionDisplayFormatter(context.LocalizationService))
    {
        MacroName = "Recover Failed Export";

        foreach (var action in DesignPreviewSamples.CreateEditorActions())
        {
            Actions.Add(action);
        }

        foreach (var warning in DesignPreviewSamples.CreateEditorWarnings())
        {
            LoadWarnings.Add(warning);
        }

        SelectedAction = Actions.FirstOrDefault(action => action.Type == EditorActionType.MouseClick);
        SkipInitialZeroZero = true;
    }
}
