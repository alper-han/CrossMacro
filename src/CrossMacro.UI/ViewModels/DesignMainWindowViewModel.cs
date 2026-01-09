using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.UI.Services;

namespace CrossMacro.UI.ViewModels;

/// <summary>
/// Design-time ViewModel for XAML preview in IDE
/// </summary>
public class DesignMainWindowViewModel : MainWindowViewModel
{
    public DesignMainWindowViewModel() : base(
        new DesignRecordingViewModel(),
        new DesignPlaybackViewModel(),
        new DesignFilesViewModel(),
        new DesignTextExpansionViewModel(),
        new DesignScheduleViewModel(),
        new DesignShortcutViewModel(),
        new DesignSettingsViewModel(),
        new DesignEditorViewModel(),
        new MockGlobalHotkeyService(),
        new MockMousePositionProvider(),
        new MockEnvironmentInfoProvider(),
        null)
    {
    }

    private class MockEnvironmentInfoProvider : IEnvironmentInfoProvider
    {
        public DisplayEnvironment CurrentEnvironment => DisplayEnvironment.Windows;
        public bool WindowManagerHandlesCloseButton => false;
    }

    private class DesignRecordingViewModel : RecordingViewModel
    {
        public DesignRecordingViewModel() : base(
            new MockMacroRecorder(),
            new MockGlobalHotkeyService(),
            new MockSettingsService())
        {
        }
    }

    private class DesignPlaybackViewModel : PlaybackViewModel
    {
        public DesignPlaybackViewModel() : base(
            new MockMacroPlayer(),
            new MockSettingsService())
        {
        }
    }

    private class DesignFilesViewModel : FilesViewModel
    {
        public DesignFilesViewModel() : base(new MockMacroFileManager(), new MockDialogService())
        {
        }
    }

    private class DesignEditorViewModel : EditorViewModel
    {
        public DesignEditorViewModel() : base(
            new MockEditorActionConverter(),
            new MockEditorActionValidator(),
            new MockCoordinateCaptureService(),
            new MockMacroFileManager(),
            new MockDialogService(),
            new MockKeyCodeMapper())
        {
        }
    }

    private class MockKeyCodeMapper : IKeyCodeMapper
    {
        public string GetKeyName(int keyCode) => $"Key{keyCode}";
        public int GetKeyCode(string keyName) => 0;
        public bool IsModifierKeyCode(int code) => false;
        public int GetKeyCodeForCharacter(char character) => character;
        public bool RequiresShift(char character) => char.IsUpper(character);
        public bool RequiresAltGr(char character) => false;
        public char? GetCharacterForKeyCode(int keyCode, bool withShift = false) => (char)keyCode;
    }

    private class MockEditorActionConverter : IEditorActionConverter
    {
        public List<MacroEvent> ToMacroEvents(EditorAction action) => new();
        public EditorAction FromMacroEvent(MacroEvent ev, MacroEvent? nextEvent = null) => new() { Type = EditorActionType.Delay };
        public MacroSequence ToMacroSequence(IEnumerable<EditorAction> actions, string name, bool isAbsolute, bool skipInitialZeroZero = false) => new() { Name = name };
        public List<EditorAction> FromMacroSequence(MacroSequence sequence) => new();
    }

    private class MockEditorActionValidator : IEditorActionValidator
    {
        public (bool IsValid, string? Error) Validate(EditorAction action) => (true, null);
        public (bool IsValid, List<string> Errors) ValidateAll(IEnumerable<EditorAction> actions) => (true, new List<string>());
    }

    private class MockCoordinateCaptureService : ICoordinateCaptureService
    {
        public bool IsCapturing => false;
        public Task<(int X, int Y)?> CaptureMousePositionAsync(CancellationToken ct = default) => Task.FromResult<(int, int)?>(null);
        public Task<int?> CaptureKeyCodeAsync(CancellationToken ct = default) => Task.FromResult<int?>(null);
        public void CancelCapture() { }
    }

    private class MockDialogService : IDialogService
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
            return Task.FromResult<string?>("design_mode.macro");
        }

        public Task<string?> ShowOpenFileDialogAsync(string title, FileDialogFilter[] filters)
        {
             return Task.FromResult<string?>("design_mode.macro");
        }
    }

    private class DesignTextExpansionViewModel : TextExpansionViewModel
    {
        public DesignTextExpansionViewModel() : base(new MockTextExpansionStorageService(), new MockDialogService(), new MockEnvironmentInfoProvider())
        {
            Expansions = new ObservableCollection<TextExpansion>
            {
                new TextExpansion(":mail", "email@example.com"),
                new TextExpansion(":date", "2023-10-27", false)
            };
        }
    }


    private class DesignSettingsViewModel : SettingsViewModel
    {
        public DesignSettingsViewModel() : base(
            new MockGlobalHotkeyService(),
            new MockSettingsService(),
            new MockTextExpansionService(),
            new HotkeySettings())
        {
        }
    }

    private class DesignScheduleViewModel : ScheduleViewModel
    {
        public DesignScheduleViewModel() : base(
            new MockSchedulerService(),
            new MockDialogService())
        {
        }
    }

    private class DesignShortcutViewModel : ShortcutViewModel
    {
        public DesignShortcutViewModel() : base(
            new MockShortcutService(),
            new MockDialogService())
        {
        }
    }

    private class MockShortcutService : IShortcutService
    {
        public ObservableCollection<ShortcutTask> Tasks { get; } = new();
        public bool IsListening => false;
#pragma warning disable CS0067 // Event is never used (design-time mock)
        public event EventHandler<ShortcutExecutedEventArgs>? ShortcutExecuted;
        public event EventHandler<ShortcutTask>? ShortcutStarting;
#pragma warning restore CS0067
        public void AddTask(ShortcutTask task) => Tasks.Add(task);
        public void RemoveTask(Guid id) { }
        public void UpdateTask(ShortcutTask task) { }
        public void SetTaskEnabled(Guid id, bool enabled) { }
        public void Start() { }
        public void Stop() { }
        public Task SaveAsync() => Task.CompletedTask;
        public Task LoadAsync() => Task.CompletedTask;
        public void Dispose() { }
    }

    private class MockSchedulerService : ISchedulerService
    {
        public ObservableCollection<ScheduledTask> Tasks { get; } = new();
        public bool IsRunning => false;
#pragma warning disable CS0067 // Event is never used (design-time mock)
        public event EventHandler<TaskExecutedEventArgs>? TaskExecuted;
        public event EventHandler<ScheduledTask>? TaskStarting;
#pragma warning restore CS0067
        public void AddTask(ScheduledTask task) => Tasks.Add(task);
        public void RemoveTask(Guid id) { }
        public void UpdateTask(ScheduledTask task) { }
        public void SetTaskEnabled(Guid id, bool enabled) { }
        public void Start() { }
        public void Stop() { }
        public Task RunTaskAsync(Guid taskId) => Task.CompletedTask;
        public Task SaveAsync() => Task.CompletedTask;
        public Task LoadAsync() => Task.CompletedTask;
        public void Dispose() { }
    }

    private class MockMacroRecorder : IMacroRecorder
    {
        public bool IsRecording => false;
#pragma warning disable CS0067 // Event is never used (design-time mock)
        public event EventHandler<MacroEvent>? EventRecorded;
#pragma warning restore CS0067
        public Task StartRecordingAsync(bool recordMouse, bool recordKeyboard, IEnumerable<int>? ignoredKeys = null, bool forceRelative = false, bool skipInitialZero = false, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public MacroSequence StopRecording() => new MacroSequence();
        public MacroSequence? GetCurrentRecording() => null;
        public void Dispose() { }
    }

    private class MockMacroPlayer : IMacroPlayer
    {
        public bool IsPaused => false;
        public int CurrentLoop => 0;
        public int TotalLoops => 0;
        public bool IsWaitingBetweenLoops => false;
        public Task PlayAsync(MacroSequence macro, PlaybackOptions? options = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Stop() { }
        public void Pause() { }
        public void Resume() { }
        public void Dispose() { }
    }

    private class MockMacroFileManager : IMacroFileManager
    {
        public Task SaveAsync(MacroSequence macro, string filePath) => Task.CompletedTask;
        public Task<MacroSequence?> LoadAsync(string filePath) => Task.FromResult<MacroSequence?>(null);
    }

    private class MockGlobalHotkeyService : IGlobalHotkeyService
    {
#pragma warning disable CS0067 // Events are never used (design-time mock)
        public event EventHandler? ToggleRecordingRequested;
        public event EventHandler? TogglePlaybackRequested;
        public event EventHandler? TogglePauseRequested;
        public event EventHandler<RawHotkeyInputEventArgs>? RawInputReceived;
        public event EventHandler<string>? ErrorOccurred;
#pragma warning restore CS0067
        public int RecordingHotkeyCode => 0;
        public int PlaybackHotkeyCode => 0;
        public int PauseHotkeyCode => 0;
        public string? LastError => null;
        public bool IsRunning => false;
        public void Start() { }
        public void Stop() { }
        public void UpdateHotkeys(string recordingHotkey, string playbackHotkey, string pauseHotkey) { }
        public Task<string> CaptureNextKeyAsync(CancellationToken cancellationToken = default) => Task.FromResult(string.Empty);
        public void SetPlaybackPauseHotkeysEnabled(bool enabled) { }
        public void Dispose() { }
    }

    private class MockMousePositionProvider : IMousePositionProvider
    {
        public string ProviderName => "Design";
        public bool IsSupported => true;
        public Task<(int X, int Y)?> GetAbsolutePositionAsync() => Task.FromResult<(int X, int Y)?>((0, 0));
        public Task<(int Width, int Height)?> GetScreenResolutionAsync() => Task.FromResult<(int Width, int Height)?>((1920, 1080));
        public void Dispose() { }
    }

    private class MockTextExpansionStorageService : Infrastructure.Services.TextExpansionStorageService
    {
        public MockTextExpansionStorageService() : base()
        {
        }
    }

    private class MockSettingsService : ISettingsService
    {
        public AppSettings Current { get; } = new AppSettings();
        public Task<AppSettings> LoadAsync() => Task.FromResult(Current);
        public AppSettings Load() => Current;
        public Task SaveAsync() => Task.CompletedTask;
        public void Save() { }
    }

    private class MockTextExpansionService : ITextExpansionService
    {
        public bool IsRunning => false;
        public void Start() { }
        public void Stop() { }
        public void Dispose() { }
    }


}
