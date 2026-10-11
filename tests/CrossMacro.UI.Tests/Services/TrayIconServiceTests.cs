using Avalonia.Controls.Platform;
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
                new FakeTrayHostMonitor(isAvailable: true), new FakeRuntimeContext());
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

    [Fact]
    public void IsTraySupported_WhenRuntimeContextIsFlatpak_ReturnsFalse()
    {
        var runtimeContext = new FakeRuntimeContext { IsFlatpak = true };
        Assert.False(TrayIconService.IsTraySupported(runtimeContext));
    }

    [Fact]
    public void IsTraySupported_WhenRuntimeContextIsNotFlatpak_ReturnsTrue()
    {
        var runtimeContext = new FakeRuntimeContext { IsFlatpak = false };
        Assert.True(TrayIconService.IsTraySupported(runtimeContext));
    }

    private static DesktopStartupPreferences MinimizedPreferences => new(
        ShouldStartMinimized: true, PersistTrayEnabled: true, UseStartupTrayOnly: false);

    private static async Task WaitUntilAsync(Func<bool> condition, CancellationToken token)
    {
        while (!condition()) { await Task.Delay(1, token); }
    }

    private sealed class TrayWindowFixture : IAsyncDisposable
    {
        private readonly IDisposable _platformScope;
        private readonly TrayIcons? _originalIcons;
        private readonly DesktopLifetimeContext _context = new();
        private readonly TrayWindowingPlatform _trayPlatform;

        public ClassicDesktopStyleApplicationLifetime Desktop { get; } = new()
        {
            ShutdownMode = ShutdownMode.OnLastWindowClose,
        };
        public MainWindow Window { get; }
        public MainWindowViewModel ViewModel { get; }
        public FakeTrayHostMonitor Monitor { get; }
        public TrayIconService Tray { get; }

        public TrayWindowFixture(bool nativeTray, bool hostAvailable, IMacroPlayer? player = null, ISettingsService? settings = null)
        {
            var originalPlatform = AvaloniaLocator.Current.GetRequiredService<IWindowingPlatform>();
            _platformScope = AvaloniaLocator.EnterScope();
            _trayPlatform = new TrayWindowingPlatform(originalPlatform, nativeTray);
            AvaloniaLocator.CurrentMutable.Bind<IWindowingPlatform>().ToConstant(_trayPlatform);
            _originalIcons = TrayIcon.GetIcons(Avalonia.Application.Current!);
            ViewModel = MainWindowViewModelTests.CreateMainWindowViewModel(macroPlayer: player, settingsService: settings);
            Window = new MainWindow { DataContext = ViewModel };
            _context.Attach(Desktop);
            _context.SetMainWindow(Window);
            Monitor = new FakeTrayHostMonitor(hostAvailable);
            var localization = Substitute.For<ILocalizationService>();
            localization[Arg.Any<string>()].Returns(call => call.Arg<string>());
            Tray = new TrayIconService(_context, ViewModel, localization, Monitor, new FakeRuntimeContext());
        }

        public void ClickNativeTray() =>
            (_trayPlatform.CreatedTray?.OnClicked
                ?? throw new InvalidOperationException("No attached native tray click callback.")).Invoke();

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
            try
            {
                await Tray.DisposeAsync();
                Window.Close();
                ViewModel.Dispose();
                Desktop.Dispose();
            }
            finally
            {
                TrayIcon.SetIcons(Avalonia.Application.Current!, _originalIcons);
                _platformScope.Dispose();
            }
        }
    }

    // Only native tray creation changes; real headless windows retain their implementation.
    private sealed class TrayWindowingPlatform(IWindowingPlatform headlessPlatform, bool nativeTray) : IWindowingPlatform
    {
        public NativeTrayIcon? CreatedTray { get; private set; }
        public IWindowImpl CreateWindow() => headlessPlatform.CreateWindow();
        public ITopLevelImpl CreateEmbeddableTopLevel() => headlessPlatform.CreateEmbeddableTopLevel();
        public IWindowImpl CreateEmbeddableWindow() => headlessPlatform.CreateEmbeddableWindow();
        public void GetWindowsZOrder(ReadOnlySpan<IWindowImpl> windows, Span<long> zOrder) =>
            headlessPlatform.GetWindowsZOrder(windows, zOrder);
        public ITrayIconImpl? CreateTrayIcon() => CreatedTray = nativeTray ? new NativeTrayIcon() : null;
    }

    private sealed class NativeTrayIcon : ITrayIconImpl
    {
        public INativeMenuExporter MenuExporter { get; } = Substitute.For<INativeMenuExporter>();
        public Action? OnClicked { get; set; }
        public void SetIcon(IWindowIconImpl? icon) { }
        public void SetToolTipText(string? text) { }
        public void SetIsVisible(bool visible) { }
        public void Dispose() => OnClicked = null;
    }

    private sealed class FakeTrayHostMonitor(bool isAvailable) : ITrayHostMonitor
    {
        public bool IsAvailable { get; private set; } = isAvailable;
        public event EventHandler? AvailabilityChanged;
        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void SetAvailable(bool available)
        {
            IsAvailable = available;
            AvailabilityChanged?.Invoke(this, EventArgs.Empty);
        }
        public void Dispose() { }
    }

    private sealed class FakeRuntimeContext : IRuntimeContext
    {
        public bool IsLinux => true;
        public bool IsWindows => false;
        public bool IsMacOS => false;
        public bool IsFlatpak { get; set; }
        public string? SessionType => "wayland";
    }
}
