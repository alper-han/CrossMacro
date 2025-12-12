using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Core.Wayland;
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
        new DesignSettingsViewModel(),
        new MockGlobalHotkeyService(),
        new MockMousePositionProvider())
    {
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
        public DesignFilesViewModel() : base(new MockMacroFileManager())
        {
        }
    }

    private class DesignTextExpansionViewModel : TextExpansionViewModel
    {
        public DesignTextExpansionViewModel() : base(new MockTextExpansionStorageService(), new MockDialogService())
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
            new HotkeySettings())
        {
        }
    }

    private class MockMacroRecorder : IMacroRecorder
    {
        public bool IsRecording => false;
#pragma warning disable CS0067 // Event is never used (design-time mock)
        public event EventHandler<MacroEvent>? EventRecorded;
#pragma warning restore CS0067
        public Task StartRecordingAsync(bool recordMouse, bool recordKeyboard, IEnumerable<int>? ignoredKeys = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
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
#pragma warning restore CS0067
        public int RecordingHotkeyCode => 0;
        public int PlaybackHotkeyCode => 0;
        public int PauseHotkeyCode => 0;
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

    private class MockDialogService : IDialogService
    {
        public Task<bool> ShowConfirmationAsync(string title, string message, string yesText = "Yes", string noText = "No")
        {
            return Task.FromResult(true);
        }
    }
}
