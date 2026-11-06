using Avalonia.Platform;
using CrossMacro.UI.Tests.ViewModels;
using CrossMacro.UI.Views;

namespace CrossMacro.UI.Tests.Services;

[Collection("Headless startup")]
public sealed class TrayIconServiceTests
{
    [Fact]
    public Task StartMinimized_WithoutNativeTray_RemainsAccessibleFromTaskbar() =>
        DesktopQuickSetupDialogTests.RunHeadlessAsync(async token =>
        {
            using var viewModel = MainWindowViewModelTests.CreateMainWindowViewModel();
            await using var tray = new TrayIconService(
                new DesktopLifetimeContext(), viewModel, Substitute.For<ILocalizationService>(),
                new FakeTrayHostMonitor(isAvailable: true), new AvaloniaTrayIconBackend());
            await tray.InitializeAsync(token);

            var plan = DesktopStartupRuntimeService.CreateDisplayPlan(
                MinimizedPreferences, tray.IsAvailable);

            Assert.Equal(DesktopStartupDisplayMode.Minimized, plan.InitialDisplayMode);
            Assert.True(plan.ShowInTaskbar);
            Assert.Equal(WindowState.Minimized, plan.InitialState);
            Assert.Equal(ShutdownMode.OnLastWindowClose, plan.ShutdownMode);
        });

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public Task StartMinimized_RequiresBothNativeAttachmentAndHost(
        bool nativeTray, bool hostAvailable, bool windowVisible) =>
        DesktopQuickSetupDialogTests.RunHeadlessAsync(async token =>
        {
            await using var fixture = new TrayWindowFixture(nativeTray, hostAvailable);
            await using var runtime = fixture.CreateRuntime();

            await runtime.StartAsync(fixture.Desktop, MinimizedPreferences, startInputServices: false).WaitAsync(token);

            Assert.Equal(windowVisible, fixture.Window.IsVisible);
            Assert.Equal(windowVisible, fixture.Window.ShowInTaskbar);
            Assert.Equal(windowVisible ? WindowState.Minimized : WindowState.Normal, fixture.Window.WindowState);
            Assert.Equal(windowVisible ? ShutdownMode.OnLastWindowClose : ShutdownMode.OnExplicitShutdown,
                fixture.Desktop.ShutdownMode);
        });

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public Task Close_WithoutUsableTray_ClosesInsteadOfStrandingHiddenWindow(
        bool nativeTray, bool hostAvailable, bool shouldClose) =>
        DesktopQuickSetupDialogTests.RunHeadlessAsync(async token =>
        {
            await using var fixture = new TrayWindowFixture(nativeTray, hostAvailable);
            await fixture.Tray.InitializeAsync(token);
            fixture.Tray.SetEnabled(enabled: true);
            fixture.Window.Show();
            var closed = false;
            fixture.Window.Closed += (_, _) => closed = true;

            fixture.Window.Close();

            Assert.Equal(shouldClose, closed);
            Assert.False(fixture.Window.IsVisible);
            Assert.Equal(shouldClose ? ShutdownMode.OnLastWindowClose : ShutdownMode.OnExplicitShutdown,
                fixture.Desktop.ShutdownMode);
        });

    [Theory]
    [InlineData(nameof(SettingsViewModel.EnableTrayIcon))]
    [InlineData(nameof(SettingsViewModel.StartMinimized))]
    [InlineData(nameof(SettingsViewModel.HideToTrayOnPlayback))]
    [InlineData(nameof(SettingsViewModel.HideToTrayOnRecording))]
    public Task EnablingTrayPreference_WhileSavePending_ImmediatelyAllowsCloseToTray(string preference) =>
        DesktopQuickSetupDialogTests.RunHeadlessAsync(async token =>
        {
            var save = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var settings = CrossMacro.Tests.SettingsServiceSubstitute.Create();
            settings.Current.Returns(new AppSettings { EnableTrayIcon = false, CheckForUpdates = false });
            settings.SaveAfterIdleAsync().Returns(save.Task);
            await using var fixture = new TrayWindowFixture(nativeTray: true, hostAvailable: true, settings: settings);
            await using var runtime = fixture.CreateRuntime();
            await runtime.StartAsync(fixture.Desktop,
                new(ShouldStartMinimized: false, PersistTrayEnabled: false, UseStartupTrayOnly: false), startInputServices: false).WaitAsync(token);
            var closed = false;
            fixture.Window.Closed += (_, _) => closed = true;
            try
            {
                switch (preference)
                {
                    case nameof(SettingsViewModel.EnableTrayIcon): fixture.ViewModel.Settings.EnableTrayIcon = true; break;
                    case nameof(SettingsViewModel.StartMinimized): fixture.ViewModel.Settings.StartMinimized = true; break;
                    case nameof(SettingsViewModel.HideToTrayOnPlayback): fixture.ViewModel.Settings.HideToTrayOnPlayback = true; break;
                    case nameof(SettingsViewModel.HideToTrayOnRecording): fixture.ViewModel.Settings.HideToTrayOnRecording = true; break;
                }
                fixture.Window.Close();
                Assert.False(closed);
                Assert.False(fixture.Window.IsVisible);
                Assert.Equal(ShutdownMode.OnExplicitShutdown, fixture.Desktop.ShutdownMode);
                Assert.False(save.Task.IsCompleted);
                fixture.ClickNativeTray();
                Assert.True(fixture.Window.IsVisible);
            }
            finally
            {
                save.TrySetResult();
                if (fixture.ViewModel.Settings.SettingsPersistenceTask is { } persistence) { await persistence.WaitAsync(token); }
            }
        });


    [Fact]
    public Task EnablingTray_WhenPendingSaveFails_RestoresRuntimeAndHiddenWindow() =>
        DesktopQuickSetupDialogTests.RunHeadlessAsync(async token =>
        {
            var save = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var settings = CrossMacro.Tests.SettingsServiceSubstitute.Create();
            settings.Current.Returns(new AppSettings { EnableTrayIcon = false, CheckForUpdates = false });
            settings.SaveAfterIdleAsync().Returns(save.Task);
            await using var fixture = new TrayWindowFixture(nativeTray: true, hostAvailable: true, settings: settings);
            await using var runtime = fixture.CreateRuntime();
            await runtime.StartAsync(fixture.Desktop,
                new(ShouldStartMinimized: false, PersistTrayEnabled: false, UseStartupTrayOnly: false), startInputServices: false).WaitAsync(token);
            fixture.ViewModel.Settings.EnableTrayIcon = true;
            Assert.True(fixture.Tray.IsAvailable);
            fixture.Window.Close();
            Assert.False(fixture.Window.IsVisible);

            save.SetException(new IOException("disk full"));
            await fixture.ViewModel.Settings.SettingsPersistenceTask!.WaitAsync(token);
            await WaitUntilAsync(() => !fixture.Tray.IsAvailable && fixture.Window.IsVisible, token);

            Assert.False(settings.Current.EnableTrayIcon);
            Assert.False(fixture.Tray.IsAvailable);
            Assert.True(fixture.Window.IsVisible);
            Assert.True(fixture.Window.ShowInTaskbar);
        });

    [Fact]
    public Task StartupWindow_WhileHostProbePending_PreservesCloseToTrayPreference() =>
        DesktopQuickSetupDialogTests.RunHeadlessAsync(async token =>
        {
            var hostRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var settings = CrossMacro.Tests.SettingsServiceSubstitute.Create();
            settings.Current.Returns(new AppSettings { EnableTrayIcon = true, CheckForUpdates = false });
            await using var fixture = new TrayWindowFixture(nativeTray: true, hostAvailable: true,
                settings: settings, hostStartTask: hostRelease.Task);
            await using var runtime = fixture.CreateRuntime();
            var startup = runtime.StartAsync(fixture.Desktop,
                new(ShouldStartMinimized: false, PersistTrayEnabled: true, UseStartupTrayOnly: false), startInputServices: false);
            var closed = false;
            fixture.Window.Closed += (_, _) => closed = true;
            try
            {
                await WaitUntilAsync(() => fixture.Window.IsVisible, token);
                Assert.False(startup.IsCompleted);
                fixture.Window.Close();
                Assert.False(closed);
                Assert.False(fixture.Window.IsVisible);
                Assert.Equal(ShutdownMode.OnExplicitShutdown, fixture.Desktop.ShutdownMode);
            }
            finally
            {
                hostRelease.TrySetResult();
                await startup.WaitAsync(token);
            }
            fixture.ClickNativeTray();
            Assert.True(fixture.Window.IsVisible);
        });

    [Fact]
    public Task StartupWindow_WhileHostProbePending_RestoresIfHostIsUnavailable() =>
        DesktopQuickSetupDialogTests.RunHeadlessAsync(async token =>
        {
            var hostRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            await using var fixture = new TrayWindowFixture(nativeTray: true, hostAvailable: false, hostStartTask: hostRelease.Task);
            await using var runtime = fixture.CreateRuntime();
            var startup = runtime.StartAsync(fixture.Desktop,
                new(ShouldStartMinimized: false, PersistTrayEnabled: true, UseStartupTrayOnly: false), startInputServices: false);
            var closed = false;
            fixture.Window.Closed += (_, _) => closed = true;
            try
            {
                await WaitUntilAsync(() => fixture.Window.IsVisible, token);
                fixture.Window.Close();
                Assert.False(closed);
                Assert.False(fixture.Window.IsVisible);
            }
            finally
            {
                hostRelease.TrySetResult();
                await startup.WaitAsync(token);
            }
            await WaitUntilAsync(() => fixture.Window.IsVisible, token);
            Assert.True(fixture.Window.ShowInTaskbar);
        });

    [Fact]
    public Task RegistrationLoss_WithHealthyHost_RestoresHiddenWindow() =>
        DesktopQuickSetupDialogTests.RunHeadlessAsync(async token =>
        {
            await using var fixture = new TrayWindowFixture(nativeTray: true, hostAvailable: true);
            await fixture.Tray.InitializeAsync(token);
            fixture.Tray.SetEnabled(true);
            fixture.Window.Show();
            fixture.Window.Close();
            Assert.False(fixture.Window.IsVisible);

            fixture.SetTrayReady(false);

            Assert.False(fixture.Tray.IsAvailable);
            Assert.True(fixture.Window.IsVisible);
            Assert.True(fixture.Window.ShowInTaskbar);
        });

    [Fact]
    public Task RegistrationFailure_WithHealthyHost_DoesNotHideWindowOnClose() =>
        DesktopQuickSetupDialogTests.RunHeadlessAsync(async token =>
        {
            await using var fixture = new TrayWindowFixture(nativeTray: true, hostAvailable: true);
            await fixture.Tray.InitializeAsync(token);
            fixture.SetTrayReady(false);
            fixture.Tray.SetEnabled(true);
            fixture.Window.Show();
            var closed = false;
            fixture.Window.Closed += (_, _) => closed = true;

            fixture.Window.Close();

            Assert.True(closed);
            Assert.False(fixture.Tray.IsAvailable);
        });


    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public Task RecordingHide_RequiresUsableTray_AndStopRestoresAccess(
        bool nativeTray, bool hostAvailable, bool visibleDuringRecording) =>
        DesktopQuickSetupDialogTests.RunHeadlessAsync(async token =>
        {
            await using var fixture = new TrayWindowFixture(nativeTray, hostAvailable);
            fixture.ViewModel.Settings.HideToTrayOnRecording = true;
            await fixture.Tray.InitializeAsync(token);
            fixture.Tray.SetEnabled(enabled: true);
            fixture.Window.Show();

            await fixture.ViewModel.Recording.StartRecordingAsync().WaitAsync(token);
            Assert.True(fixture.ViewModel.Recording.IsRecording);
            await WaitUntilAsync(() => fixture.Window.IsVisible == visibleDuringRecording, token);
            Assert.Equal(visibleDuringRecording ? ShutdownMode.OnLastWindowClose : ShutdownMode.OnExplicitShutdown,
                fixture.Desktop.ShutdownMode);

            fixture.ViewModel.Recording.StopRecording();

            await WaitUntilAsync(() => fixture.Window.IsVisible, token);
            Assert.True(fixture.Window.ShowInTaskbar);
            Assert.Equal(ShutdownMode.OnLastWindowClose, fixture.Desktop.ShutdownMode);
        });

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public Task PlaybackHide_RequiresUsableTray_AndCompletionRestoresAccess(
        bool nativeTray, bool hostAvailable, bool visibleDuringPlayback) =>
        DesktopQuickSetupDialogTests.RunHeadlessAsync(async token =>
        {
            var finishPlayback = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var player = Substitute.For<IMacroPlayer>();
            player.PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>())
                .Returns(finishPlayback.Task);
            await using var fixture = new TrayWindowFixture(nativeTray, hostAvailable, player);
            fixture.ViewModel.Settings.HideToTrayOnPlayback = true;
            fixture.ViewModel.Playback.CountdownSeconds = 0;
            fixture.ViewModel.Playback.IsLooping = false;
            fixture.ViewModel.Playback.SetMacro(new MacroSequence
            {
                Name = "Tray access regression",
                Events = { new MacroEvent { Type = EventType.KeyPress, KeyCode = 30 } },
            });
            await fixture.Tray.InitializeAsync(token);
            fixture.Tray.SetEnabled(enabled: true);
            fixture.Window.Show();

            var playback = fixture.ViewModel.Playback.PlayMacroAsync();
            try
            {
                await WaitUntilAsync(() => fixture.ViewModel.Playback.IsPlaying, token);
                await WaitUntilAsync(() => fixture.Window.IsVisible == visibleDuringPlayback, token);
                Assert.Equal(visibleDuringPlayback ? ShutdownMode.OnLastWindowClose : ShutdownMode.OnExplicitShutdown,
                    fixture.Desktop.ShutdownMode);
            }
            finally
            {
                finishPlayback.TrySetResult();
                await playback.WaitAsync(token);
            }

            await WaitUntilAsync(() => fixture.Window.IsVisible, token);
            Assert.True(fixture.Window.ShowInTaskbar);
            Assert.Equal(ShutdownMode.OnLastWindowClose, fixture.Desktop.ShutdownMode);
        });

    [Fact]
    public Task TrayClick_RestoresStartupHiddenWindowToTaskbar() =>
        DesktopQuickSetupDialogTests.RunHeadlessAsync(async token =>
        {
            await using var fixture = new TrayWindowFixture(nativeTray: true, hostAvailable: true);
            await using var runtime = fixture.CreateRuntime();
            await runtime.StartAsync(fixture.Desktop, MinimizedPreferences, startInputServices: false).WaitAsync(token);
            Assert.False(fixture.Window.IsVisible);
            Assert.False(fixture.Window.ShowInTaskbar);

            fixture.ClickNativeTray();

            Assert.True(fixture.Window.IsVisible);
            Assert.True(fixture.Window.ShowInTaskbar);
            Assert.Equal(WindowState.Normal, fixture.Window.WindowState);
            Assert.Equal(ShutdownMode.OnLastWindowClose, fixture.Desktop.ShutdownMode);
        });

    [Fact]
    public Task HostLoss_RestoresHiddenReadyStartupWindow_HostReturnDoesNotHideAgain() =>
        DesktopQuickSetupDialogTests.RunHeadlessAsync(async token =>
        {
            await using var fixture = new TrayWindowFixture(nativeTray: true, hostAvailable: true);
            await using var runtime = fixture.CreateRuntime();
            await runtime.StartAsync(fixture.Desktop, MinimizedPreferences, startInputServices: false).WaitAsync(token);
            Assert.False(fixture.Window.IsVisible);
            Assert.False(fixture.Window.ShowInTaskbar);
            Assert.True(fixture.ViewModel.StartupInitializationTask.IsCompletedSuccessfully);
            Assert.False(fixture.ViewModel.Playback.IsPlaying);
            Assert.False(fixture.ViewModel.Recording.IsRecording);

            await Task.Run(() => fixture.Monitor.SetAvailable(available: false), cancellationToken: token);

            await WaitUntilAsync(() => fixture.Window.IsVisible, token);
            Assert.True(fixture.Window.ShowInTaskbar);
            Assert.Equal(WindowState.Normal, fixture.Window.WindowState);
            Assert.Equal(ShutdownMode.OnLastWindowClose, fixture.Desktop.ShutdownMode);

            fixture.Monitor.SetAvailable(available: true);
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => { });

            Assert.True(fixture.Window.IsVisible);
            Assert.True(fixture.Window.ShowInTaskbar);
            Assert.Equal(ShutdownMode.OnLastWindowClose, fixture.Desktop.ShutdownMode);
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task DisablingOrHidingTray_RestoresHiddenReadyWindow(bool hideIcon) =>
        DesktopQuickSetupDialogTests.RunHeadlessAsync(async token =>
        {
            await using var fixture = new TrayWindowFixture(nativeTray: true, hostAvailable: true);
            await using var runtime = fixture.CreateRuntime();
            await runtime.StartAsync(fixture.Desktop, MinimizedPreferences, startInputServices: false).WaitAsync(token);
            Assert.False(fixture.Window.IsVisible);

            if (hideIcon) { fixture.Tray.Hide(); }
            else { fixture.Tray.SetEnabled(enabled: false); }

            await WaitUntilAsync(() => fixture.Window.IsVisible, token);
            Assert.True(fixture.Window.ShowInTaskbar);
            Assert.Equal(ShutdownMode.OnLastWindowClose, fixture.Desktop.ShutdownMode);
        });

    private static DesktopStartupPreferences MinimizedPreferences => new(
        ShouldStartMinimized: true, PersistTrayEnabled: true, UseStartupTrayOnly: false);

    private static async Task WaitUntilAsync(Func<bool> condition, CancellationToken token)
    {
        while (!condition()) { await Task.Delay(1, token); }
    }

    private sealed class TrayWindowFixture : IAsyncDisposable
    {
        private readonly DesktopLifetimeContext _context = new();
        private readonly FakeTrayIconBackend _trayBackend;
        public ClassicDesktopStyleApplicationLifetime Desktop { get; } = new()
        {
            ShutdownMode = ShutdownMode.OnLastWindowClose,
        };
        public MainWindow Window { get; }
        public MainWindowViewModel ViewModel { get; }
        public FakeTrayHostMonitor Monitor { get; }
        public TrayIconService Tray { get; }

        public TrayWindowFixture(bool nativeTray, bool hostAvailable, IMacroPlayer? player = null, ISettingsService? settings = null, Task? hostStartTask = null)
        {
            _trayBackend = new FakeTrayIconBackend(nativeTray);
            ViewModel = MainWindowViewModelTests.CreateMainWindowViewModel(macroPlayer: player, settingsService: settings);
            Window = new MainWindow { DataContext = ViewModel };
            _context.Attach(Desktop);
            _context.SetMainWindow(Window);
            Monitor = new FakeTrayHostMonitor(hostAvailable, hostStartTask);
            var localization = Substitute.For<ILocalizationService>();
            localization[Arg.Any<string>()].Returns(call => call.Arg<string>());
            Tray = new TrayIconService(_context, ViewModel, localization, Monitor, _trayBackend);
        }

        public void ClickNativeTray() =>
            (_trayBackend.CreatedTray
                ?? throw new InvalidOperationException("No attached native tray click callback.")).Click();

        public void SetTrayReady(bool ready) => _trayBackend.CreatedTray!.SetReady(ready);

        public DesktopStartupRuntimeService CreateRuntime() => new(
            getMainWindow: () => Window,
            getTrayIconService: () => Tray,
            getMainWindowViewModel: () => ViewModel,
            getInputSimulatorPool: () => null,
            getPositionProvider: () => null,
            desktopLifetimeContext: _context,
            runtimeLifecycle: Substitute.For<IRuntimeLifecycle>());

        public async ValueTask DisposeAsync()
        {
            await Tray.DisposeAsync();
            Window.Close();
            ViewModel.Dispose();
            Desktop.Dispose();
        }
    }

    private sealed class FakeTrayIconBackend(bool nativeTray) : ITrayIconBackend
    {
        public FakeTrayIconHandle? CreatedTray { get; private set; }
        public ITrayIconHandle Create(WindowIcon icon, NativeMenu menu, string toolTipText)
        {
            CreatedTray = nativeTray ? new FakeTrayIconHandle() : null;
            return CreatedTray is null ? new FakeUnavailableTrayIconHandle() : CreatedTray;
        }
    }

    private sealed class FakeTrayIconHandle : ITrayIconHandle
    {
        public event EventHandler? Clicked;
        public event EventHandler? AvailabilityChanged;
        public bool IsReady { get; private set; } = true;
        public Task WaitForReadyAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public void SetReady(bool ready)
        {
            IsReady = ready;
            AvailabilityChanged?.Invoke(this, EventArgs.Empty);
        }
        public bool IsVisible { get; set; }
        public bool HasNativeMenuExporter => true;
        public string? GetToolTipText() => null;
        public void SetToolTipText(string? value) { }
        public void Click() => Clicked?.Invoke(this, EventArgs.Empty);
        public void Dispose() => Clicked = null;
    }

    private sealed class FakeUnavailableTrayIconHandle : ITrayIconHandle
    {
        public event EventHandler? Clicked { add { } remove { } }
        public event EventHandler? AvailabilityChanged { add { } remove { } }
        public bool IsReady => false;
        public Task WaitForReadyAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public bool IsVisible { get; set; }
        public bool HasNativeMenuExporter => false;
        public string? GetToolTipText() => null;
        public void SetToolTipText(string? value) { }
        public void Dispose() { }
    }

    private sealed class FakeTrayHostMonitor(bool isAvailable, Task? startTask = null) : ITrayHostMonitor
    {
        public bool IsAvailable { get; private set; } = isAvailable;
        public event EventHandler? AvailabilityChanged;
        public Task StartAsync(CancellationToken cancellationToken = default) => startTask ?? Task.CompletedTask;
        public void SetAvailable(bool available)
        {
            IsAvailable = available;
            AvailabilityChanged?.Invoke(this, EventArgs.Empty);
        }
        public void Dispose() { }
    }

}
