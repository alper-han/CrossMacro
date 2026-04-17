using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Services;
using CrossMacro.UI.Services;

namespace CrossMacro.UI.ViewModels;

internal sealed class DesignPreviewContext
{
    public DesignPreviewContext()
    {
        SettingsService = new DesignSettingsService(CreateSettings());
        HotkeySettings = CreateHotkeySettings();
        HotkeyService = new DesignGlobalHotkeyService();
        MousePositionProvider = new DesignMousePositionProvider();
        EnvironmentInfoProvider = new DesignEnvironmentInfoProvider();
        RuntimeContext = new DesignRuntimeContext();
        ExternalUrlOpener = new DesignExternalUrlOpener();
        ThemeService = new DesignThemeService(SettingsService.Current.Theme);
        DialogService = new DesignDialogService();
        LoadedMacroSession = new LoadedMacroSession();
        TextExpansionStorageService = new DesignTextExpansionStorageService();
        TextExpansionService = new DesignTextExpansionService();
        SchedulerService = new DesignSchedulerService();
        ShortcutService = new DesignShortcutService();
        MacroRecorder = new DesignMacroRecorder();
        MacroPlayer = new DesignMacroPlayer();
        MacroFileManager = new DesignMacroFileManager();
        EditorActionConverter = new DesignEditorActionConverter();
        EditorActionValidator = new DesignEditorActionValidator();
        CoordinateCaptureService = new DesignCoordinateCaptureService();
        KeyCodeMapper = new DesignKeyCodeMapper();
        TimeProvider = new DesignTimeProvider();
    }

    public DesignSettingsService SettingsService { get; }

    public HotkeySettings HotkeySettings { get; }

    public DesignGlobalHotkeyService HotkeyService { get; }

    public DesignMousePositionProvider MousePositionProvider { get; }

    public DesignEnvironmentInfoProvider EnvironmentInfoProvider { get; }

    public DesignRuntimeContext RuntimeContext { get; }

    public DesignExternalUrlOpener ExternalUrlOpener { get; }

    public DesignThemeService ThemeService { get; }

    public DesignDialogService DialogService { get; }

    public LoadedMacroSession LoadedMacroSession { get; }

    public DesignTextExpansionStorageService TextExpansionStorageService { get; }

    public DesignTextExpansionService TextExpansionService { get; }

    public DesignSchedulerService SchedulerService { get; }

    public DesignShortcutService ShortcutService { get; }

    public DesignMacroRecorder MacroRecorder { get; }

    public DesignMacroPlayer MacroPlayer { get; }

    public DesignMacroFileManager MacroFileManager { get; }

    public DesignEditorActionConverter EditorActionConverter { get; }

    public DesignEditorActionValidator EditorActionValidator { get; }

    public DesignCoordinateCaptureService CoordinateCaptureService { get; }

    public DesignKeyCodeMapper KeyCodeMapper { get; }

    public DesignTimeProvider TimeProvider { get; }

    private static AppSettings CreateSettings()
    {
        return new AppSettings
        {
            EnableTrayIcon = true,
            StartMinimized = true,
            PlaybackSpeed = 1.25,
            IsLooping = true,
            LoopCount = 3,
            LoopDelayMs = 250,
            CountdownSeconds = 3,
            IsMouseRecordingEnabled = true,
            IsKeyboardRecordingEnabled = true,
            ForceRelativeCoordinates = true,
            SkipInitialZeroZero = true,
            EnableTextExpansion = true,
            CheckForUpdates = true,
            LogLevel = "Information",
            Theme = "Nord"
        };
    }

    private static HotkeySettings CreateHotkeySettings()
    {
        return new HotkeySettings
        {
            RecordingHotkey = "Ctrl+Alt+R",
            PlaybackHotkey = "Ctrl+Alt+P",
            PauseHotkey = "Ctrl+Alt+Space"
        };
    }
}

internal static class DesignPreviewSamples
{
    public static readonly DateTime SampleNow = new(2026, 4, 16, 9, 30, 0, DateTimeKind.Local);

    public static MacroSequence CreateMacro(string name = "Invoice Form Fill")
    {
        var macro = new MacroSequence
        {
            Name = name,
            CreatedAt = SampleNow,
            RecordedAt = SampleNow.AddMinutes(-15),
            ActualDuration = TimeSpan.FromSeconds(2),
            IsAbsoluteCoordinates = false,
            SkipInitialZeroZero = true,
            MouseMoveCount = 1,
            ClickCount = 1,
            EventsPerSecond = 3.0,
            TrailingDelayMs = 200,
            Events =
            [
                new MacroEvent { Type = EventType.MouseMove, X = 420, Y = 180, Timestamp = 0, DelayMs = 80 },
                new MacroEvent { Type = EventType.Click, X = 420, Y = 180, Button = MouseButton.Left, Timestamp = 80, DelayMs = 120 },
                new MacroEvent { Type = EventType.KeyPress, KeyCode = 30, Timestamp = 200, DelayMs = 30 },
                new MacroEvent { Type = EventType.KeyRelease, KeyCode = 30, Timestamp = 230, DelayMs = 30 },
                new MacroEvent { Type = EventType.KeyPress, KeyCode = 48, Timestamp = 260, DelayMs = 30 },
                new MacroEvent { Type = EventType.KeyRelease, KeyCode = 48, Timestamp = 290, DelayMs = 200 }
            ]
        };

        macro.CalculateDuration();
        return macro;
    }

    public static IReadOnlyList<TextExpansion> CreateTextExpansions()
    {
        return
        [
            new TextExpansion(":sync-ok", "Inventory sync completed successfully", true, PasteMethod.CtrlV, TextInsertionMode.Paste),
            new TextExpansion(":retry-note", "Retry failed upload after reconnecting VPN", true, PasteMethod.CtrlShiftV, TextInsertionMode.Paste),
            new TextExpansion(":runbook", "Open dashboard and start the nightly export macro", true, PasteMethod.CtrlV, TextInsertionMode.DirectTyping)
        ];
    }

    public static IReadOnlyList<ScheduledTask> CreateScheduledTasks()
    {
        var intervalTask = new ScheduledTask
        {
            Name = "Refresh warehouse dashboard",
            Type = ScheduleType.Interval,
            MacroFilePath = "/home/demo/macros/refresh-dashboard.macro",
            PlaybackSpeed = 1.2,
            IntervalValue = 15,
            IntervalUnit = IntervalUnit.Minutes,
            LastRunTime = SampleNow.AddMinutes(-10),
            LastStatus = "Last run completed"
        };
        intervalTask.IsEnabled = true;
        intervalTask.NextRunTime = SampleNow.AddMinutes(5);

        var oneShotTask = new ScheduledTask
        {
            Name = "Run nightly export",
            Type = ScheduleType.SpecificTime,
            MacroFilePath = "/home/demo/macros/run-nightly-export.macro",
            PlaybackSpeed = 1.0,
            ScheduledDateTime = SampleNow.Date.AddDays(1).AddHours(1),
            LastStatus = "Queued for scheduled run"
        };
        oneShotTask.IsEnabled = true;
        oneShotTask.NextRunTime = SampleNow.Date.AddDays(1).AddHours(1);

        return [intervalTask, oneShotTask];
    }

    public static IReadOnlyList<ShortcutTask> CreateShortcutTasks()
    {
        var loopShortcut = new ShortcutTask
        {
            Name = "Hold to repeat click",
            MacroFilePath = "/home/demo/macros/repeat-click.macro",
            HotkeyString = "Ctrl+Shift+1",
            PlaybackSpeed = 1.4,
            LoopEnabled = true,
            RepeatCount = 0,
            RepeatDelayMs = 120,
            LastTriggeredTime = SampleNow.AddMinutes(-3),
            LastStatus = "Loop running"
        };
        loopShortcut.IsEnabled = true;

        var singleShortcut = new ShortcutTask
        {
            Name = "Run invoice entry macro",
            MacroFilePath = "/home/demo/macros/invoice-entry.macro",
            HotkeyString = "Ctrl+Alt+H",
            PlaybackSpeed = 1.0,
            LastTriggeredTime = SampleNow.AddHours(-2),
            LastStatus = "Completed"
        };
        singleShortcut.IsEnabled = true;

        return [loopShortcut, singleShortcut];
    }

    public static IReadOnlyList<EditorAction> CreateEditorActions()
    {
        return
        [
            new EditorAction
            {
                Type = EditorActionType.SetVariable,
                ScriptVariableName = "retryCount",
                ScriptValueType = ScriptValueType.Number,
                ScriptValue = "0"
            },
            new EditorAction
            {
                Type = EditorActionType.IfBlockStart,
                ScriptLeftOperandType = ScriptOperandType.VariableReference,
                ScriptLeftOperand = "retryCount",
                ScriptConditionOperator = ScriptConditionOperator.LessThan,
                ScriptRightOperandType = ScriptOperandType.Number,
                ScriptRightOperand = "3"
            },
            new EditorAction
            {
                Type = EditorActionType.MouseClick,
                Button = MouseButton.Left,
                UseCurrentPosition = true,
                IsAbsolute = false
            },
            new EditorAction
            {
                Type = EditorActionType.Delay,
                DelayMs = 250
            },
            new EditorAction
            {
                Type = EditorActionType.TextInput,
                Text = "Export completed"
            },
            new EditorAction
            {
                Type = EditorActionType.BlockEnd
            }
        ];
    }

    public static IReadOnlyList<string> CreateEditorWarnings()
    {
        return
        [
            "Step 7: Unsupported script line was kept as raw text for preview"
        ];
    }
}

internal sealed class DesignEnvironmentInfoProvider : IEnvironmentInfoProvider
{
    public DisplayEnvironment CurrentEnvironment => DisplayEnvironment.LinuxGnome;

    public bool WindowManagerHandlesCloseButton => false;
}

internal sealed class DesignRuntimeContext : IRuntimeContext
{
    public bool IsLinux => true;

    public bool IsWindows => false;

    public bool IsMacOS => false;

    public bool IsFlatpak => false;

    public string? SessionType => "x11";
}

internal sealed class DesignSettingsService : ISettingsService
{
    public DesignSettingsService(AppSettings settings)
    {
        Current = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public AppSettings Current { get; }

    public Task<AppSettings> LoadAsync() => Task.FromResult(Current);

    public AppSettings Load() => Current;

    public Task SaveAsync() => Task.CompletedTask;

    public void Save()
    {
    }
}

internal sealed class DesignGlobalHotkeyService : IGlobalHotkeyService
{
    public int RecordingHotkeyCode => 19;

    public int PlaybackHotkeyCode => 25;

    public int PauseHotkeyCode => 57;

    public event EventHandler? ToggleRecordingRequested
    {
        add { }
        remove { }
    }

    public event EventHandler? TogglePlaybackRequested
    {
        add { }
        remove { }
    }

    public event EventHandler? TogglePauseRequested
    {
        add { }
        remove { }
    }

    public event EventHandler<RawHotkeyInputEventArgs>? RawInputReceived
    {
        add { }
        remove { }
    }

    public event EventHandler<RawHotkeyInputEventArgs>? RawKeyReleased
    {
        add { }
        remove { }
    }

    public event EventHandler<string>? ErrorOccurred
    {
        add { }
        remove { }
    }

    public string? LastError => null;

    public bool IsRunning { get; private set; }

    public void Start() => IsRunning = true;

    public void Stop() => IsRunning = false;

    public void UpdateHotkeys(string recordingHotkey, string playbackHotkey, string pauseHotkey)
    {
    }

    public Task<string> CaptureNextKeyAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult("Ctrl+Alt+R");
    }

    public void SetPlaybackPauseHotkeysEnabled(bool enabled)
    {
    }

    public void Dispose()
    {
    }
}

internal sealed class DesignMousePositionProvider : IMousePositionProvider
{
    public string ProviderName => "Design Preview";

    public bool IsSupported => true;

    public Task<(int X, int Y)?> GetAbsolutePositionAsync() => Task.FromResult<(int X, int Y)?>((640, 360));

    public Task<(int Width, int Height)?> GetScreenResolutionAsync() => Task.FromResult<(int Width, int Height)?>((1920, 1080));

    public void Dispose()
    {
    }
}

internal sealed class DesignExternalUrlOpener : IExternalUrlOpener
{
    public void Open(string url)
    {
    }
}

internal sealed class DesignThemeService : IThemeService
{
    public DesignThemeService(string initialTheme)
    {
        ThemeCatalog.TryResolve(initialTheme, out var descriptor);
        CurrentTheme = descriptor.Name;
    }

    public IReadOnlyList<string> AvailableThemes => ThemeCatalog.ThemeNames;

    public string CurrentTheme { get; private set; }

    public bool TryApplyTheme(string themeName, out string error)
    {
        ThemeCatalog.TryResolve(themeName, out var descriptor);
        CurrentTheme = descriptor.Name;
        error = string.Empty;
        return true;
    }
}

internal sealed class DesignDialogService : IDialogService
{
    public Task<bool> ShowConfirmationAsync(string title, string message, string yesText = "Yes", string noText = "No")
    {
        return Task.FromResult(true);
    }

    public Task ShowMessageAsync(string title, string message, string buttonText = "OK")
    {
        return Task.CompletedTask;
    }

    public Task<string?> ShowSaveFileDialogAsync(string title, string defaultFileName, FileDialogFilter[] filters)
    {
        return Task.FromResult<string?>("/home/demo/macros/nightly-export-retry.macro");
    }

    public Task<string?> ShowOpenFileDialogAsync(string title, FileDialogFilter[] filters)
    {
        return Task.FromResult<string?>("/home/demo/macros/nightly-export-retry.macro");
    }
}

internal sealed class DesignTextExpansionStorageService : ITextExpansionStorageService
{
    private readonly object _sync = new();
    private List<TextExpansion> _expansions = new();

    public List<TextExpansion> Load() => GetCurrent();

    public Task<List<TextExpansion>> LoadAsync() => Task.FromResult(GetCurrent());

    public Task SaveAsync(IEnumerable<TextExpansion> expansions)
    {
        ArgumentNullException.ThrowIfNull(expansions);

        lock (_sync)
        {
            _expansions = expansions.Select(CloneExpansion).ToList();
        }

        return Task.CompletedTask;
    }

    public List<TextExpansion> GetCurrent()
    {
        lock (_sync)
        {
            return _expansions.Select(CloneExpansion).ToList();
        }
    }

    public string FilePath => "design://text-expansions.json";

    private static TextExpansion CloneExpansion(TextExpansion expansion)
    {
        return new TextExpansion(expansion.Trigger, expansion.Replacement, expansion.IsEnabled, expansion.Method, expansion.InsertionMode);
    }
}

internal sealed class DesignTextExpansionService : ITextExpansionService
{
    public bool IsRunning { get; private set; }

    public void Start() => IsRunning = true;

    public void Stop() => IsRunning = false;

    public void Dispose()
    {
    }
}

internal sealed class DesignSchedulerService : ISchedulerService
{
    public DesignSchedulerService()
    {
        Tasks = new ObservableCollection<ScheduledTask>(DesignPreviewSamples.CreateScheduledTasks());
    }

    public ObservableCollection<ScheduledTask> Tasks { get; }

    public bool IsRunning { get; private set; }

    public event EventHandler<TaskExecutedEventArgs>? TaskExecuted;

    public event EventHandler<ScheduledTask>? TaskStarting;

    public void AddTask(ScheduledTask task) => Tasks.Add(task);

    public void RemoveTask(Guid id)
    {
        var task = Tasks.FirstOrDefault(item => item.Id == id);
        if (task != null)
        {
            Tasks.Remove(task);
        }
    }

    public void UpdateTask(ScheduledTask task)
    {
    }

    public void SetTaskEnabled(Guid id, bool enabled)
    {
        var task = Tasks.FirstOrDefault(item => item.Id == id);
        if (task != null)
        {
            task.IsEnabled = enabled;
        }
    }

    public Task RunTaskAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        var task = Tasks.FirstOrDefault(item => item.Id == taskId);
        if (task != null)
        {
            TaskStarting?.Invoke(this, task);
            TaskExecuted?.Invoke(this, new TaskExecutedEventArgs(task, success: true, message: "Preview macro run completed"));
        }

        return Task.CompletedTask;
    }

    public void Start() => IsRunning = true;

    public void Stop() => IsRunning = false;

    public Task SaveAsync() => Task.CompletedTask;

    public Task LoadAsync() => Task.CompletedTask;

    public void Dispose()
    {
    }
}

internal sealed class DesignShortcutService : IShortcutService
{
    public DesignShortcutService()
    {
        Tasks = new ObservableCollection<ShortcutTask>(DesignPreviewSamples.CreateShortcutTasks());
    }

    public ObservableCollection<ShortcutTask> Tasks { get; }

    public bool IsListening { get; private set; }

    public event EventHandler<ShortcutExecutedEventArgs>? ShortcutExecuted;

    public event EventHandler<ShortcutTask>? ShortcutStarting;

    public void AddTask(ShortcutTask task) => Tasks.Add(task);

    public void RemoveTask(Guid id)
    {
        var task = Tasks.FirstOrDefault(item => item.Id == id);
        if (task != null)
        {
            Tasks.Remove(task);
        }
    }

    public void UpdateTask(ShortcutTask task)
    {
    }

    public void SetTaskEnabled(Guid id, bool enabled)
    {
        var task = Tasks.FirstOrDefault(item => item.Id == id);
        if (task != null)
        {
            task.IsEnabled = enabled;
        }
    }

    public void Start() => IsListening = true;

    public void Stop() => IsListening = false;

    public Task SaveAsync() => Task.CompletedTask;

    public Task RunTaskAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        var task = Tasks.FirstOrDefault(item => item.Id == taskId);
        if (task != null)
        {
            ShortcutStarting?.Invoke(this, task);
            ShortcutExecuted?.Invoke(this, new ShortcutExecutedEventArgs(task, success: true, message: "Preview macro run completed"));
        }

        return Task.CompletedTask;
    }

    public Task LoadAsync() => Task.CompletedTask;

    public void Dispose()
    {
    }
}

internal sealed class DesignMacroRecorder : IMacroRecorder
{
    public bool IsRecording { get; private set; }

    public event EventHandler<MacroEvent>? EventRecorded
    {
        add { }
        remove { }
    }

    public Task StartRecordingAsync(bool recordMouse, bool recordKeyboard, IEnumerable<int>? ignoredKeys = null, bool forceRelative = false, bool skipInitialZero = false, CancellationToken cancellationToken = default)
    {
        IsRecording = true;
        return Task.CompletedTask;
    }

    public MacroSequence StopRecording()
    {
        IsRecording = false;
        return DesignPreviewSamples.CreateMacro();
    }

    public MacroSequence? GetCurrentRecording() => DesignPreviewSamples.CreateMacro();

    public void Dispose()
    {
    }
}

internal sealed class DesignMacroPlayer : IMacroPlayer
{
    public bool IsPaused { get; private set; }

    public int CurrentLoop { get; private set; }

    public int TotalLoops { get; private set; }

    public bool IsWaitingBetweenLoops { get; private set; }

    public Task PlayAsync(MacroSequence macro, PlaybackOptions? options = null, CancellationToken cancellationToken = default)
    {
        TotalLoops = options?.Loop == true ? Math.Max(1, options?.RepeatCount ?? 1) : 1;
        CurrentLoop = 1;
        IsPaused = false;
        IsWaitingBetweenLoops = false;
        return Task.CompletedTask;
    }

    public void Stop()
    {
        CurrentLoop = 0;
        TotalLoops = 0;
        IsPaused = false;
        IsWaitingBetweenLoops = false;
    }

    public void Pause() => IsPaused = true;

    public void Resume() => IsPaused = false;

    public void Dispose()
    {
    }
}

internal sealed class DesignMacroFileManager : IMacroFileManager
{
    public Task SaveAsync(MacroSequence macro, string filePath) => Task.CompletedTask;

    public Task<MacroSequence?> LoadAsync(string filePath) => Task.FromResult<MacroSequence?>(DesignPreviewSamples.CreateMacro("Loaded Nightly Export Retry"));
}

internal sealed class DesignEditorActionConverter : IEditorActionConverter
{
    public List<MacroEvent> ToMacroEvents(EditorAction action) => new();

    public EditorAction FromMacroEvent(MacroEvent ev, MacroEvent? nextEvent = null) => new() { Type = EditorActionType.Delay, DelayMs = ev.DelayMs };

    public MacroSequence ToMacroSequence(IEnumerable<EditorAction> actions, string name, bool isAbsolute, bool skipInitialZeroZero = false)
    {
        var macro = DesignPreviewSamples.CreateMacro(name);
        macro.IsAbsoluteCoordinates = isAbsolute;
        macro.SkipInitialZeroZero = skipInitialZeroZero;
        return macro;
    }

    public List<EditorAction> FromMacroSequence(MacroSequence sequence) => DesignPreviewSamples.CreateEditorActions().ToList();

    public EditorActionRestoreResult FromMacroSequenceWithDiagnostics(MacroSequence sequence)
    {
        return new EditorActionRestoreResult(DesignPreviewSamples.CreateEditorActions().ToList(), new List<EditorActionRestoreWarning>(), restoredFromScriptSteps: true);
    }
}

internal sealed class DesignEditorActionValidator : IEditorActionValidator
{
    public (bool IsValid, string? Error) Validate(EditorAction action) => (true, null);

    public (bool IsValid, List<string> Errors) ValidateAll(IEnumerable<EditorAction> actions) => (true, new List<string>());
}

internal sealed class DesignCoordinateCaptureService : ICoordinateCaptureService
{
    public bool IsCapturing => false;

    public Task<(int X, int Y)?> CaptureMousePositionAsync(CancellationToken ct = default) => Task.FromResult<(int X, int Y)?>((640, 360));

    public Task<int?> CaptureKeyCodeAsync(CancellationToken ct = default) => Task.FromResult<int?>(30);

    public void CancelCapture()
    {
    }
}

internal sealed class DesignKeyCodeMapper : IKeyCodeMapper
{
    public string GetKeyName(int keyCode) => $"Key{keyCode}";

    public int GetKeyCode(string keyName) => 0;

    public bool IsModifierKeyCode(int code) => false;

    public int GetKeyCodeForCharacter(char character) => character;

    public bool RequiresShift(char character) => char.IsUpper(character);

    public bool RequiresAltGr(char character) => false;

    public char? GetCharacterForKeyCode(int keyCode, bool withShift = false) => (char)keyCode;
}

internal sealed class DesignTimeProvider : ITimeProvider
{
    public DateTime Now => DesignPreviewSamples.SampleNow;

    public DateTime UtcNow => DesignPreviewSamples.SampleNow.ToUniversalTime();
}
