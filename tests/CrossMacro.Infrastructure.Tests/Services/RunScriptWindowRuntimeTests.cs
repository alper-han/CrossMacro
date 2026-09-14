
namespace CrossMacro.Infrastructure.Tests.Services;

public sealed class RunScriptWindowRuntimeTests
{
    [Fact]
    public async Task NullWindowManager_WhenCancellationIsAlreadyRequested_ThrowsBeforeReportingUnsupported()
    {
        var warnings = new List<string>();
        var manager = new NullWindowManager(warnings.Add);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var operations = new Func<Task>[]
        {
            () => manager.GetActiveWindowAsync(cancellation.Token),
            () => manager.GetWindowsAsync(cancellation.Token),
            () => manager.FocusWindowByAddressAsync("0x1", cancellation.Token),
            () => manager.FocusWindowByTitleAsync("title", cancellation.Token),
            () => manager.FocusWindowByClassAsync("class", cancellation.Token),
            () => manager.CloseWindowByAddressAsync("0x1", cancellation.Token),
            () => manager.CloseWindowByTitleAsync("title", cancellation.Token),
            () => manager.MoveActiveWindowAsync(1, 2, cancellation.Token),
            () => manager.ResizeActiveWindowAsync(3, 4, cancellation.Token),
            () => manager.MaximizeActiveWindowAsync(cancellation.Token),
            () => manager.FullscreenActiveWindowAsync(cancellation.Token),
            () => manager.FloatActiveWindowAsync(cancellation.Token),
            () => manager.CenterActiveWindowAsync(cancellation.Token),
            () => manager.GetActiveWorkspaceAsync(cancellation.Token),
            () => manager.SwitchWorkspaceAsync("workspace", cancellation.Token),
            () => manager.MoveActiveWindowToWorkspaceAsync("workspace", cancellation.Token),
            () => manager.MoveWindowToWorkspaceByAddressAsync("0x1", "workspace", cancellation.Token),
        };

        foreach (var operation in operations)
        {
            _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(operation);
        }

        Assert.Empty(warnings);
    }

    // ---- active -----------------------------------------------------------------------

    [Fact]
    public async Task ExecuteStepAsync_WhenActiveTitle_StoresTitleInVariable()
    {
        var wm = FakeWindowManager(new WindowInfo { Title = "My Window", Class = "myapp", Address = "0x1234" });
        var vars = Vars();

        await Executor(wm).ExecuteStepAsync("window active title result", 1, vars, CancellationToken.None);

        _ = vars.Should().Contain("result", "My Window");
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenActiveClass_StoresClassInVariable()
    {
        var wm = FakeWindowManager(new WindowInfo { Title = "My Window", Class = "myapp", Address = "0x1234" });
        var vars = Vars();

        await Executor(wm).ExecuteStepAsync("window active class result", 1, vars, CancellationToken.None);

        _ = vars.Should().Contain("result", "myapp");
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenActiveAddress_StoresAddressInVariable()
    {
        var wm = FakeWindowManager(new WindowInfo { Title = "My Window", Class = "myapp", Address = "0x1234" });
        var vars = Vars();

        await Executor(wm).ExecuteStepAsync("window active address result", 1, vars, CancellationToken.None);

        _ = vars.Should().Contain("result", "0x1234");
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenActiveState_StoresBooleanInVariable()
    {
        var wm = FakeWindowManager(new WindowInfo { IsFullscreen = true, IsFloating = false });
        var vars = Vars();

        await Executor(wm).ExecuteStepAsync("window active fullscreen res1", 1, vars, CancellationToken.None);
        await Executor(wm).ExecuteStepAsync("window active float res2", 1, vars, CancellationToken.None);

        _ = vars.Should().Contain("res1", "true");
        _ = vars.Should().Contain("res2", "false");
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenActiveExtendedStateAndGeometry_StoresAllValues()
    {
        var wm = FakeWindowManager(new WindowInfo
        {
            IsMaximized = true,
            IsPinned = true,
            IsHidden = true,
            X = -10,
            Y = 20,
            Width = 800,
            Height = 600,
        });
        var vars = Vars();

        await Executor(wm).ExecuteStepAsync("window active maximize maximized", 1, vars, CancellationToken.None);
        await Executor(wm).ExecuteStepAsync("window active pinned pinned", 1, vars, CancellationToken.None);
        await Executor(wm).ExecuteStepAsync("window active hidden hidden", 1, vars, CancellationToken.None);
        await Executor(wm).ExecuteStepAsync("window active geometry geometry", 1, vars, CancellationToken.None);

        _ = vars.Should().Contain("maximized", "true");
        _ = vars.Should().Contain("pinned", "true");
        _ = vars.Should().Contain("hidden", "true");
        _ = vars.Should().Contain("geometry", "-10 20 800 600");
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenActiveWithNoWindow_StoresEmptyString()
    {
        var wm = FakeWindowManager(activeWindow: null);
        var vars = Vars();

        await Executor(wm).ExecuteStepAsync("window active title result", 1, vars, CancellationToken.None);

        _ = vars.Should().Contain("result", string.Empty);
    }

    // ---- search -----------------------------------------------------------------------

    [Fact]
    public async Task ExecuteStepAsync_WhenSearchByTitle_StoresMatchingWindowAddress()
    {
        var wm = FakeWindowList(
            new WindowInfo { Title = "Firefox", Class = "firefox", Address = "0xAABB" },
            new WindowInfo { Title = "Terminal", Class = "alacritty", Address = "0xCCDD" });
        var vars = Vars();

        await Executor(wm).ExecuteStepAsync("window search title Firefox result", 1, vars, CancellationToken.None);

        _ = vars.Should().Contain("result", "0xAABB");
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenSearchByClass_StoresMatchingWindowAddress()
    {
        var wm = FakeWindowList(
            new WindowInfo { Title = "My Term", Class = "alacritty", Address = "0xCCDD" });
        var vars = Vars();

        await Executor(wm).ExecuteStepAsync("window search class alacritty result", 1, vars, CancellationToken.None);

        _ = vars.Should().Contain("result", "0xCCDD");
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenSearchFindsNoMatch_StoresEmptyString()
    {
        var wm = FakeWindowList(
            new WindowInfo { Title = "Terminal", Class = "alacritty", Address = "0xCCDD" });
        var vars = Vars();

        await Executor(wm).ExecuteStepAsync("window search title missing result", 1, vars, CancellationToken.None);

        _ = vars.Should().Contain("result", string.Empty);
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenSearchByTitleQuoted_StripsQuotesAndMatches()
    {
        var wm = FakeWindowList(
            new WindowInfo { Title = "Code Editor", Class = "code", Address = "0xEEFF" });
        var vars = Vars();

        await Executor(wm).ExecuteStepAsync("window search title \"Code Editor\" result", 1, vars, CancellationToken.None);

        _ = vars.Should().Contain("result", "0xEEFF");
    }

    // ---- focus ------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteStepAsync_WhenFocusActive_FocusesActiveWindowByAddress()
    {
        var wm = FakeWindowManager(new WindowInfo { Address = "0x5678", Title = "T", Class = "C" });
        _ = wm.FocusWindowByAddressAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(returnThis: true);

        await Executor(wm).ExecuteStepAsync("window focus active", 1, Vars(), CancellationToken.None);

        _ = await wm.Received(1).FocusWindowByAddressAsync("0x5678", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenFocusActiveHasNoWindow_DoesNotMutate()
    {
        var wm = FakeWindowManager(activeWindow: null);

        await Executor(wm).ExecuteStepAsync("window focus active", 1, Vars(), CancellationToken.None);

        _ = await wm.DidNotReceive().FocusWindowByAddressAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenFocusByTitle_CallsFocusWindowByTitle()
    {
        var wm = FakeWindowManager(activeWindow: null);
        _ = wm.FocusWindowByTitleAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(returnThis: true);

        await Executor(wm).ExecuteStepAsync("window focus title Firefox", 1, Vars(), CancellationToken.None);

        _ = await wm.Received(1).FocusWindowByTitleAsync("Firefox", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenFocusByQuotedTitleWithEscapes_CallsFocusWindowByTitle()
    {
        var wm = FakeWindowManager(activeWindow: null);
        _ = wm.FocusWindowByTitleAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(returnThis: true);

        await Executor(wm).ExecuteStepAsync("window focus title \"Fire\\\" Fox\"", 1, Vars(), CancellationToken.None);

        _ = await wm.Received(1).FocusWindowByTitleAsync("Fire\" Fox", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenFocusByClass_CallsFocusWindowByClass()
    {
        var wm = FakeWindowManager(activeWindow: null);
        _ = wm.FocusWindowByClassAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(returnThis: true);

        await Executor(wm).ExecuteStepAsync("window focus class alacritty", 1, Vars(), CancellationToken.None);

        _ = await wm.Received(1).FocusWindowByClassAsync("alacritty", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenFocusByAddress_CallsFocusWindowByAddress()
    {
        var wm = FakeWindowManager(activeWindow: null);
        _ = wm.FocusWindowByAddressAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(returnThis: true);

        await Executor(wm).ExecuteStepAsync("window focus address 0xABCD", 1, Vars(), CancellationToken.None);

        _ = await wm.Received(1).FocusWindowByAddressAsync("0xABCD", Arg.Any<CancellationToken>());
    }

    // ---- close ------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteStepAsync_WhenCloseActive_ClosesActiveWindowByAddress()
    {
        var wm = FakeWindowManager(new WindowInfo { Address = "0x9ABC", Title = "T", Class = "C" });
        _ = wm.CloseWindowByAddressAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(returnThis: true);

        await Executor(wm).ExecuteStepAsync("window close active", 1, Vars(), CancellationToken.None);

        _ = await wm.Received(1).CloseWindowByAddressAsync("0x9ABC", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenCloseActiveHasNoWindow_DoesNotMutate()
    {
        var wm = FakeWindowManager(activeWindow: null);

        await Executor(wm).ExecuteStepAsync("window close active", 1, Vars(), CancellationToken.None);

        _ = await wm.DidNotReceive().CloseWindowByAddressAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenCloseByTitle_CallsCloseWindowByTitle()
    {
        var wm = FakeWindowManager(activeWindow: null);
        _ = wm.CloseWindowByTitleAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(returnThis: true);

        await Executor(wm).ExecuteStepAsync("window close title notepad", 1, Vars(), CancellationToken.None);

        _ = await wm.Received(1).CloseWindowByTitleAsync("notepad", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenCloseByQuotedTitle_PreservesWhitespace()
    {
        var wm = FakeWindowManager(activeWindow: null);
        _ = wm.CloseWindowByTitleAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(returnThis: true);

        await Executor(wm).ExecuteStepAsync("window close title \"Code Editor\"", 1, Vars(), CancellationToken.None);

        _ = await wm.Received(1).CloseWindowByTitleAsync("Code Editor", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenCloseByAddress_CallsCloseWindowByAddress()
    {
        var wm = FakeWindowManager(activeWindow: null);
        _ = wm.CloseWindowByAddressAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(returnThis: true);

        await Executor(wm).ExecuteStepAsync("window close address 0xDEAD", 1, Vars(), CancellationToken.None);

        _ = await wm.Received(1).CloseWindowByAddressAsync("0xDEAD", Arg.Any<CancellationToken>());
    }


    // ---- move / resize / state --------------------------------------------------------

    [Fact]
    public async Task ExecuteStepAsync_WhenMove_CallsMoveActiveWindowWithCoordinates()
    {
        var wm = FakeWindowManager(activeWindow: null);
        _ = wm.MoveActiveWindowAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(returnThis: true);

        await Executor(wm).ExecuteStepAsync("window move 100 200", 1, Vars(), CancellationToken.None);

        _ = await wm.Received(1).MoveActiveWindowAsync(100, 200, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenMoveUnlocksWindow_UsesInjectedDelayBeforeMutation()
    {
        var wm = FakeWindowManager(new WindowInfo
        {
            IsFullscreen = true,
            IsMaximized = true,
            IsFloating = false,
            Address = "0x1234",
        });
        _ = wm.FullscreenActiveWindowAsync(Arg.Any<CancellationToken>()).Returns(returnThis: true);
        _ = wm.MaximizeActiveWindowAsync(Arg.Any<CancellationToken>()).Returns(returnThis: true);
        _ = wm.FloatActiveWindowAsync(Arg.Any<CancellationToken>()).Returns(returnThis: true);
        _ = wm.MoveActiveWindowAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(returnThis: true);
        var delayStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseDelay = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var executor = new RunScriptWindowExecutor(
            wm,
            delayAsync: async (delay, cancellationToken) =>
            {
                Assert.Equal(TimeSpan.FromMilliseconds(150), delay);
                delayStarted.TrySetResult();
                await releaseDelay.Task.WaitAsync(cancellationToken);
            });

        var operation = executor.ExecuteStepAsync("window move 100 200", 1, Vars(), CancellationToken.None);
        await delayStarted.Task;
        _ = await wm.DidNotReceive().MoveActiveWindowAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());

        _ = releaseDelay.TrySetResult();
        await operation;
        _ = await wm.Received(1).MoveActiveWindowAsync(100, 200, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenResize_CallsResizeActiveWindowWithDimensions()
    {
        var wm = FakeWindowManager(activeWindow: null);
        _ = wm.ResizeActiveWindowAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(returnThis: true);

        await Executor(wm).ExecuteStepAsync("window resize 1280 720", 1, Vars(), CancellationToken.None);

        _ = await wm.Received(1).ResizeActiveWindowAsync(1280, 720, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenFullscreen_CallsFullscreenActiveWindow()
    {
        var wm = FakeWindowManager(activeWindow: null);
        _ = wm.FullscreenActiveWindowAsync(Arg.Any<CancellationToken>()).Returns(returnThis: true);

        await Executor(wm).ExecuteStepAsync("window fullscreen", 1, Vars(), CancellationToken.None);

        _ = await wm.Received(1).FullscreenActiveWindowAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenFloat_CallsFloatActiveWindow()
    {
        var wm = FakeWindowManager(activeWindow: null);
        _ = wm.FloatActiveWindowAsync(Arg.Any<CancellationToken>()).Returns(returnThis: true);

        await Executor(wm).ExecuteStepAsync("window float", 1, Vars(), CancellationToken.None);

        _ = await wm.Received(1).FloatActiveWindowAsync(Arg.Any<CancellationToken>());
    }

    // ---- wait -------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteStepAsync_WhenWaitFindsWindow_StoresAddressAndReturns()
    {
        var wm = FakeWindowList(new WindowInfo { Title = "Firefox", Class = "firefox", Address = "0xAABB" });
        var vars = Vars();

        await Executor(wm).ExecuteStepAsync("window wait title Firefox 1000 result", 1, vars, CancellationToken.None);

        _ = vars.Should().Contain("result", "0xAABB");
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenWaitTimesOut_UsesInjectedLogicalTimeDeterministically()
    {
        var timeProvider = new FakeTimeProvider();
        var delayRegistered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var wm = FakeWindowList();
        var executor = new RunScriptWindowExecutor(
            wm,
            timeProvider,
            (delay, cancellationToken) =>
            {
                var pendingDelay = Task.Delay(delay, timeProvider, cancellationToken);
                _ = delayRegistered.TrySetResult();
                return pendingDelay;
            });
        var vars = Vars();

        var cancellationToken = TestContext.Current.CancellationToken;
        var operation = executor.ExecuteStepAsync("window wait title missing 1000 result", 1, vars, cancellationToken);
        await delayRegistered.Task.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, cancellationToken);
        timeProvider.Advance(TimeSpan.FromSeconds(1));
        await operation.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, cancellationToken);

        _ = vars.Should().Contain("result", string.Empty);
    }

    // ---- workspace --------------------------------------------------------------------

    [Fact]
    public async Task ExecuteStepAsync_WhenGetDesktop_StoresActiveWorkspace()
    {
        var wm = FakeWindowManager(activeWindow: null);
        _ = wm.GetActiveWorkspaceAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<string?>("2"));
        var vars = Vars();

        await Executor(wm).ExecuteStepAsync("window getdesktop result", 1, vars, CancellationToken.None);

        _ = vars.Should().Contain("result", "2");
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenSetDesktop_CallsSwitchWorkspace()
    {
        var wm = FakeWindowManager(activeWindow: null);
        _ = wm.SwitchWorkspaceAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(returnThis: true);

        await Executor(wm).ExecuteStepAsync("window setdesktop 3", 1, Vars(), CancellationToken.None);

        _ = await wm.Received(1).SwitchWorkspaceAsync("3", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenSetDesktopForWindowActive_CallsMoveActiveWindowToWorkspace()
    {
        var wm = FakeWindowManager(activeWindow: null);
        _ = wm.MoveActiveWindowToWorkspaceAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(returnThis: true);

        await Executor(wm).ExecuteStepAsync("window setdesktopforwindow active 4", 1, Vars(), CancellationToken.None);

        _ = await wm.Received(1).MoveActiveWindowToWorkspaceAsync("4", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenSetDesktopForWindowByAddress_CallsMoveWindowToWorkspace()
    {
        var wm = FakeWindowManager(activeWindow: null);
        _ = wm.MoveWindowToWorkspaceByAddressAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(returnThis: true);

        await Executor(wm).ExecuteStepAsync("window setdesktopforwindow address 0x123 5", 1, Vars(), CancellationToken.None);

        _ = await wm.Received(1).MoveWindowToWorkspaceByAddressAsync("0x123", "5", Arg.Any<CancellationToken>());
    }

    // ---- variable resolution ----------------------------------------------------------

    [Fact]
    public async Task ExecuteStepAsync_WhenStepUsesVariable_ResolvesBeforeExecution()
    {
        var wm = FakeWindowManager(activeWindow: null);
        _ = wm.FocusWindowByAddressAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(returnThis: true);
        var vars = Vars();
        vars["addr"] = "0xBEEF";

        await Executor(wm).ExecuteStepAsync("window focus address $addr", 1, vars, CancellationToken.None);

        _ = await wm.Received(1).FocusWindowByAddressAsync("0xBEEF", Arg.Any<CancellationToken>());
    }

    // ---- static helpers ---------------------------------------------------------------

    [Theory]
    [InlineData("window active title x", true)]
    [InlineData("window focus active", true)]
    [InlineData("window move 1 2", true)]
    [InlineData("WINDOW RESIZE 10 10", true)]
    [InlineData("click left", false)]
    [InlineData("pixelcolor 1 2 x", false)]
    public void IsWindowStep_ReturnsExpected(string step, bool expected)
    {
        _ = RunScriptWindowExecutor.IsWindowStep(step).Should().Be(expected);
    }

    [Theory]
    [InlineData("window active title x", null)]
    [InlineData("window active fullscreen x", null)]
    [InlineData("window active float x", null)]
    [InlineData("window active pinned x", null)]
    [InlineData("window active hidden x", null)]
    [InlineData("window search title Firefox x", null)]
    [InlineData("window focus active", null)]
    [InlineData("window focus title Firefox", null)]
    [InlineData("window close active", null)]
    [InlineData("window wait class myapp 2000 res", null)]
    [InlineData("window wait title \"My App\" res", null)]
    [InlineData("window move 10 20", null)]
    [InlineData("window resize 800 600", null)]
    [InlineData("window resize 0 600", "positive")]
    [InlineData("window fullscreen", null)]
    [InlineData("window float active", null)]
    [InlineData("window center", null)]
    [InlineData("window getdesktop ws", null)]
    [InlineData("window setdesktop 2", null)]
    [InlineData("window setdesktopforwindow active 3", null)]
    [InlineData("window setdesktopforwindow address 0x123 4", null)]
    [InlineData("window active bad_field x", "Unknown field")]
    [InlineData("window focus title \"unterminated", "Unterminated quoted token")]
    [InlineData("window unknown x", "Unknown window sub-command")]
    public void Validate_ReturnsExpectedError(string step, string? errorFragment)
    {
        var result = RunScriptWindowExecutor.Validate(step);

        if (errorFragment is null)
        {
            _ = result.Should().BeNull();
        }
        else
        {
            _ = result.Should().Contain(errorFragment);
        }
    }

    // ---- helpers ----------------------------------------------------------------------

    private static RunScriptWindowExecutor Executor(IWindowManager wm) => new(wm);

    private static Dictionary<string, string> Vars() =>
        new(System.StringComparer.OrdinalIgnoreCase);

    private static IWindowManager FakeWindowManager(WindowInfo? activeWindow)
    {
        var wm = Substitute.For<IWindowManager>();
        _ = wm.GetActiveWindowAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<WindowInfo?>(activeWindow));
        return wm;
    }

    private static IWindowManager FakeWindowList(params WindowInfo[] windows)
    {
        var wm = Substitute.For<IWindowManager>();
        _ = wm.GetWindowsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<System.Collections.Generic.IReadOnlyList<WindowInfo>>(windows));
        return wm;
    }
}
