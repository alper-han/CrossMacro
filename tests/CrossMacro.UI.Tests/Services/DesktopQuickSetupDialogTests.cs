using Avalonia.Headless;
using Avalonia.Interactivity;
using CrossMacro.Platform.Abstractions.Setup;
using CrossMacro.UI.Views.Dialogs;

namespace CrossMacro.UI.Tests.Services;

[Collection("Headless startup")]
public sealed class DesktopQuickSetupDialogTests
{
    [Fact]
    public Task ClosingConsent_DoesNotAuthorizeOrStartRuntime() => RunHeadlessAsync(async token =>
    {
        var setup = Substitute.For<IAppImageQuickSetupService>();
        setup.ShouldPrompt().Returns(returnThis: true);
        var desktop = Substitute.For<IClassicDesktopStyleApplicationLifetime>();
        var started = false;
        var gate = new DesktopQuickSetupGateService(() => null, () => setup);
        var flow = gate.TryHandleAsync(desktop, Preferences, unsupportedSessionReason: null,
            (_, _, _) => { started = true; return Task.CompletedTask; }, token);
        var prompt = await WaitForDialogAsync(desktop, token);
        prompt.Close();
        await flow.WaitAsync(token);
        Assert.False(started);
        await setup.DidNotReceive().RunAsync(Arg.Any<CancellationToken>());
    });

    [Fact]
    public Task FailedSetup_WaitsForExplicitRetry_ThenStartsWithInput() => RunHeadlessAsync(async token =>
    {
        var setup = Substitute.For<IAppImageQuickSetupService>();
        setup.ShouldPrompt().Returns(returnThis: true);
        setup.RunAsync(Arg.Any<CancellationToken>()).Returns(
            new QuickSetupResult(QuickSetupOutcome.AuthorizationDenied, "Authorization denied."),
            new QuickSetupResult(QuickSetupOutcome.Succeeded, "Ready."));
        var desktop = Substitute.For<IClassicDesktopStyleApplicationLifetime>();
        bool? inputEnabled = null;
        var gate = new DesktopQuickSetupGateService(() => null, () => setup);
        var flow = gate.TryHandleAsync(desktop, Preferences, unsupportedSessionReason: null,
            (_, _, enabled) => { inputEnabled = enabled; return Task.CompletedTask; }, token);
        var prompt = await WaitForDialogAsync(desktop, token);
        Click(prompt, "YesButton");
        var retry = await WaitForDialogAsync(desktop, token, prompt);
        Assert.Null(inputEnabled);
        Assert.False(flow.IsCompleted);
        await setup.Received(1).RunAsync(token);
        Click(retry, "YesButton");
        await flow.WaitAsync(token);
        Assert.True(inputEnabled);
        await setup.Received(2).RunAsync(token);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ContinueWithoutAutomation_DoesNotAuthorizeAgain(bool failFirst) => RunHeadlessAsync(async token =>
    {
        var setup = Substitute.For<ILinuxDirectInputQuickSetupService>();
        setup.ShouldPromptAsync(Arg.Any<CancellationToken>()).Returns(new ValueTask<bool>(result: true));
        setup.RunAsync(Arg.Any<CancellationToken>()).Returns(new QuickSetupResult(QuickSetupOutcome.Cancelled, "Cancelled."));
        var desktop = Substitute.For<IClassicDesktopStyleApplicationLifetime>();
        bool? inputEnabled = null;
        var gate = new DesktopQuickSetupGateService(() => null, () => null, getLinuxDirectInputQuickSetupService: () => setup);
        var flow = gate.TryHandleAsync(desktop, Preferences, unsupportedSessionReason: null,
            (_, _, enabled) => { inputEnabled = enabled; return Task.CompletedTask; }, token);
        var prompt = await WaitForDialogAsync(desktop, token);
        if (failFirst)
        {
            Click(prompt, "YesButton");
            prompt = await WaitForDialogAsync(desktop, token, prompt);
            Assert.Null(inputEnabled);
        }
        Click(prompt, "NoButton");
        await flow.WaitAsync(token);
        Assert.False(inputEnabled);
        await setup.Received(failFirst ? 1 : 0).RunAsync(Arg.Any<CancellationToken>());
    });

    [Fact]
    public Task FlatpakFailure_ExitDoesNotStartRuntime() => RunHeadlessAsync(async token =>
    {
        var setup = Substitute.For<IFlatpakQuickSetupService>();
        setup.IsApplicable().Returns(returnThis: true);
        setup.RunAsync(Arg.Any<CancellationToken>()).Returns(new QuickSetupResult(QuickSetupOutcome.PrivilegeUnavailable, "No launcher."));
        var desktop = Substitute.For<IClassicDesktopStyleApplicationLifetime>();
        var started = false;
        var gate = new DesktopQuickSetupGateService(() => setup, () => null);
        var flow = gate.TryHandleAsync(desktop, Preferences, "No input access",
            (_, _, _) => { started = true; return Task.CompletedTask; }, token);
        var prompt = await WaitForDialogAsync(desktop, token);
        Click(prompt, "YesButton");
        var retry = await WaitForDialogAsync(desktop, token, prompt);
        Assert.False(started);
        Click(retry, "NoButton");
        await flow.WaitAsync(token);
        Assert.False(started);
        desktop.Received(1).Shutdown();
    });

    private static DesktopStartupPreferences Preferences => new(
        ShouldStartMinimized: false, PersistTrayEnabled: false, UseStartupTrayOnly: false);

    internal static async Task RunHeadlessAsync(Func<CancellationToken, Task> action)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        var session = HeadlessUnitTestSession.StartNew(typeof(HeadlessStartupApplication));
        try
        {
            await session.Dispatch(async () =>
            {
                await action(timeout.Token);
                return true;
            }, timeout.Token).WaitAsync(timeout.Token);
        }
        finally
        {
            // Dispatch completion may resume inline on the UI thread; never join that thread from itself.
            await Task.Run(session.Dispose, CancellationToken.None);
        }
    }

    private static async Task<ConfirmationDialog> WaitForDialogAsync(
        IClassicDesktopStyleApplicationLifetime desktop, CancellationToken token, ConfirmationDialog? previous = null)
    {
        while (true)
        {
            var dialog = desktop.MainWindow?.OwnedWindows.OfType<ConfirmationDialog>()
                .FirstOrDefault(window => window.IsVisible && !ReferenceEquals(window, previous));
            if (dialog is not null) { return dialog; }
            await Task.Delay(1, token);
        }
    }

    private static void Click(ConfirmationDialog dialog, string name) =>
        dialog.FindControl<Button>(name)!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
}
