using CrossMacro.UI.Tests.ViewModels;

namespace CrossMacro.UI.Tests.Views;

[Collection(TestCollections.HeadlessStartup)]
public sealed class MainWindowTests
{
    [Fact]
    public Task RefreshContentLayout_WhenContentIsNull_ThrowsArgumentNullException() =>
        HeadlessTestRunner.RunAsync(token =>
        {
            _ = Assert.Throws<ArgumentNullException>(
                () => CrossMacro.UI.Views.MainWindow.RefreshContentLayout(null!));
            return Task.CompletedTask;
        });

    [Fact]
    public Task RefreshContentLayout_InvalidatesAnAlreadyArrangedContentControl() =>
        HeadlessTestRunner.RunAsync(token =>
        {
            var content = new ContentControl();
            content.Measure(new Size(100, 100));
            content.Arrange(new Rect(0, 0, 100, 100));

            Assert.True(content.IsMeasureValid);
            Assert.True(content.IsArrangeValid);

            CrossMacro.UI.Views.MainWindow.RefreshContentLayout(content);

            Assert.False(content.IsMeasureValid);
            Assert.False(content.IsArrangeValid);
            return Task.CompletedTask;
        });

    [Fact]
    public Task RuntimeWithoutAutomation_ShowsInitializedUiWithoutStartingInput() =>
        HeadlessTestRunner.RunAsync(async token =>
        {
            using var harness = MainWindowHarness.Create();
            var window = new CrossMacro.UI.Views.MainWindow();
            var desktop = Substitute.For<IClassicDesktopStyleApplicationLifetime>();
            var lifecycle = Substitute.For<IRuntimeLifecycle>();
            var screenWarmed = false;
            await using var runtime = new DesktopStartupRuntimeService(
                getMainWindow: () => window,
                getTrayIconService: () => Substitute.For<ITrayIconService>(),
                getMainWindowViewModel: () => harness.ViewModel,
                getInputSimulatorPool: () => throw new InvalidOperationException("Limited startup must not resolve input devices."),
                getPositionProvider: () => throw new InvalidOperationException("Limited startup must not warm input devices."),
                desktopLifetimeContext: new DesktopLifetimeContext(),
                runtimeLifecycle: lifecycle,
                screenReadingWarmup: _ => { screenWarmed = true; return Task.CompletedTask; });
            try
            {
                await runtime.StartAsync(desktop, new DesktopStartupPreferences(
                    ShouldStartMinimized: false, PersistTrayEnabled: false, UseStartupTrayOnly: false), startInputServices: false).WaitAsync(token);
                Assert.True(window.IsVisible);
                Assert.True(harness.ViewModel.StartupInitializationTask.IsCompletedSuccessfully);
                Assert.True(screenWarmed);
                await lifecycle.DidNotReceive().StartAsync(Arg.Any<CancellationToken>());
            }
            finally
            {
                window.Close();
            }
        });
}
