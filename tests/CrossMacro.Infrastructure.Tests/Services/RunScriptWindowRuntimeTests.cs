using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Infrastructure.Services.Playback;
using FluentAssertions;
using NSubstitute;

namespace CrossMacro.Infrastructure.Tests.Services;

public sealed class RunScriptWindowRuntimeTests
{
    // ---- active -----------------------------------------------------------------------

    [Fact]
    public async Task ExecuteStepAsync_WhenActiveTitle_StoresTitleInVariable()
    {
        var wm = FakeWindowManager(new WindowInfo { Title = "My Window", Class = "myapp", Address = "0x1234" });
        var vars = Vars();

        await Executor(wm).ExecuteStepAsync("window active title result", 1, vars, CancellationToken.None);

        vars.Should().Contain("result", "My Window");
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenActiveClass_StoresClassInVariable()
    {
        var wm = FakeWindowManager(new WindowInfo { Title = "My Window", Class = "myapp", Address = "0x1234" });
        var vars = Vars();

        await Executor(wm).ExecuteStepAsync("window active class result", 1, vars, CancellationToken.None);

        vars.Should().Contain("result", "myapp");
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenActiveAddress_StoresAddressInVariable()
    {
        var wm = FakeWindowManager(new WindowInfo { Title = "My Window", Class = "myapp", Address = "0x1234" });
        var vars = Vars();

        await Executor(wm).ExecuteStepAsync("window active address result", 1, vars, CancellationToken.None);

        vars.Should().Contain("result", "0x1234");
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenActiveState_StoresBooleanInVariable()
    {
        var wm = FakeWindowManager(new WindowInfo { IsFullscreen = true, IsFloating = false });
        var vars = Vars();

        await Executor(wm).ExecuteStepAsync("window active fullscreen res1", 1, vars, CancellationToken.None);
        await Executor(wm).ExecuteStepAsync("window active float res2", 1, vars, CancellationToken.None);

        vars.Should().Contain("res1", "true");
        vars.Should().Contain("res2", "false");
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenActiveWithNoWindow_StoresEmptyString()
    {
        var wm = FakeWindowManager(null);
        var vars = Vars();

        await Executor(wm).ExecuteStepAsync("window active title result", 1, vars, CancellationToken.None);

        vars.Should().Contain("result", string.Empty);
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

        vars.Should().Contain("result", "0xAABB");
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenSearchByClass_StoresMatchingWindowAddress()
    {
        var wm = FakeWindowList(
            new WindowInfo { Title = "My Term", Class = "alacritty", Address = "0xCCDD" });
        var vars = Vars();

        await Executor(wm).ExecuteStepAsync("window search class alacritty result", 1, vars, CancellationToken.None);

        vars.Should().Contain("result", "0xCCDD");
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenSearchFindsNoMatch_StoresEmptyString()
    {
        var wm = FakeWindowList(
            new WindowInfo { Title = "Terminal", Class = "alacritty", Address = "0xCCDD" });
        var vars = Vars();

        await Executor(wm).ExecuteStepAsync("window search title missing result", 1, vars, CancellationToken.None);

        vars.Should().Contain("result", string.Empty);
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenSearchByTitleQuoted_StripsQuotesAndMatches()
    {
        var wm = FakeWindowList(
            new WindowInfo { Title = "Code Editor", Class = "code", Address = "0xEEFF" });
        var vars = Vars();

        await Executor(wm).ExecuteStepAsync("window search title \"Code Editor\" result", 1, vars, CancellationToken.None);

        vars.Should().Contain("result", "0xEEFF");
    }

    // ---- focus ------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteStepAsync_WhenFocusActive_FocusesActiveWindowByAddress()
    {
        var wm = FakeWindowManager(new WindowInfo { Address = "0x5678", Title = "T", Class = "C" });
        wm.FocusWindowByAddressAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        await Executor(wm).ExecuteStepAsync("window focus active", 1, Vars(), CancellationToken.None);

        await wm.Received(1).FocusWindowByAddressAsync("0x5678", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenFocusByTitle_CallsFocusWindowByTitle()
    {
        var wm = FakeWindowManager(null);
        wm.FocusWindowByTitleAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        await Executor(wm).ExecuteStepAsync("window focus title Firefox", 1, Vars(), CancellationToken.None);

        await wm.Received(1).FocusWindowByTitleAsync("Firefox", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenFocusByClass_CallsFocusWindowByClass()
    {
        var wm = FakeWindowManager(null);
        wm.FocusWindowByClassAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        await Executor(wm).ExecuteStepAsync("window focus class alacritty", 1, Vars(), CancellationToken.None);

        await wm.Received(1).FocusWindowByClassAsync("alacritty", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenFocusByAddress_CallsFocusWindowByAddress()
    {
        var wm = FakeWindowManager(null);
        wm.FocusWindowByAddressAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        await Executor(wm).ExecuteStepAsync("window focus address 0xABCD", 1, Vars(), CancellationToken.None);

        await wm.Received(1).FocusWindowByAddressAsync("0xABCD", Arg.Any<CancellationToken>());
    }

    // ---- close ------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteStepAsync_WhenCloseActive_ClosesActiveWindowByAddress()
    {
        var wm = FakeWindowManager(new WindowInfo { Address = "0x9ABC", Title = "T", Class = "C" });
        wm.CloseWindowByAddressAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        await Executor(wm).ExecuteStepAsync("window close active", 1, Vars(), CancellationToken.None);

        await wm.Received(1).CloseWindowByAddressAsync("0x9ABC", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenCloseByTitle_CallsCloseWindowByTitle()
    {
        var wm = FakeWindowManager(null);
        wm.CloseWindowByTitleAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        await Executor(wm).ExecuteStepAsync("window close title notepad", 1, Vars(), CancellationToken.None);

        await wm.Received(1).CloseWindowByTitleAsync("notepad", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenCloseByAddress_CallsCloseWindowByAddress()
    {
        var wm = FakeWindowManager(null);
        wm.CloseWindowByAddressAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        await Executor(wm).ExecuteStepAsync("window close address 0xDEAD", 1, Vars(), CancellationToken.None);

        await wm.Received(1).CloseWindowByAddressAsync("0xDEAD", Arg.Any<CancellationToken>());
    }


    // ---- move / resize / state --------------------------------------------------------

    [Fact]
    public async Task ExecuteStepAsync_WhenMove_CallsMoveActiveWindowWithCoordinates()
    {
        var wm = FakeWindowManager(null);
        wm.MoveActiveWindowAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(true);

        await Executor(wm).ExecuteStepAsync("window move 100 200", 1, Vars(), CancellationToken.None);

        await wm.Received(1).MoveActiveWindowAsync(100, 200, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenResize_CallsResizeActiveWindowWithDimensions()
    {
        var wm = FakeWindowManager(null);
        wm.ResizeActiveWindowAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(true);

        await Executor(wm).ExecuteStepAsync("window resize 1280 720", 1, Vars(), CancellationToken.None);

        await wm.Received(1).ResizeActiveWindowAsync(1280, 720, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenFullscreen_CallsFullscreenActiveWindow()
    {
        var wm = FakeWindowManager(null);
        wm.FullscreenActiveWindowAsync(Arg.Any<CancellationToken>()).Returns(true);

        await Executor(wm).ExecuteStepAsync("window fullscreen", 1, Vars(), CancellationToken.None);

        await wm.Received(1).FullscreenActiveWindowAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenFloat_CallsFloatActiveWindow()
    {
        var wm = FakeWindowManager(null);
        wm.FloatActiveWindowAsync(Arg.Any<CancellationToken>()).Returns(true);

        await Executor(wm).ExecuteStepAsync("window float", 1, Vars(), CancellationToken.None);

        await wm.Received(1).FloatActiveWindowAsync(Arg.Any<CancellationToken>());
    }

    // ---- wait -------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteStepAsync_WhenWaitFindsWindow_StoresAddressAndReturns()
    {
        var wm = FakeWindowList(new WindowInfo { Title = "Firefox", Class = "firefox", Address = "0xAABB" });
        var vars = Vars();

        await Executor(wm).ExecuteStepAsync("window wait title Firefox 1000 result", 1, vars, CancellationToken.None);

        vars.Should().Contain("result", "0xAABB");
    }

    // ---- workspace --------------------------------------------------------------------

    [Fact]
    public async Task ExecuteStepAsync_WhenGetDesktop_StoresActiveWorkspace()
    {
        var wm = FakeWindowManager(null);
        wm.GetActiveWorkspaceAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<string?>("2"));
        var vars = Vars();

        await Executor(wm).ExecuteStepAsync("window getdesktop result", 1, vars, CancellationToken.None);

        vars.Should().Contain("result", "2");
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenSetDesktop_CallsSwitchWorkspace()
    {
        var wm = FakeWindowManager(null);
        wm.SwitchWorkspaceAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        await Executor(wm).ExecuteStepAsync("window setdesktop 3", 1, Vars(), CancellationToken.None);

        await wm.Received(1).SwitchWorkspaceAsync("3", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenSetDesktopForWindowActive_CallsMoveActiveWindowToWorkspace()
    {
        var wm = FakeWindowManager(null);
        wm.MoveActiveWindowToWorkspaceAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        await Executor(wm).ExecuteStepAsync("window setdesktopforwindow active 4", 1, Vars(), CancellationToken.None);

        await wm.Received(1).MoveActiveWindowToWorkspaceAsync("4", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenSetDesktopForWindowByAddress_CallsMoveWindowToWorkspace()
    {
        var wm = FakeWindowManager(null);
        wm.MoveWindowToWorkspaceByAddressAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        await Executor(wm).ExecuteStepAsync("window setdesktopforwindow address 0x123 5", 1, Vars(), CancellationToken.None);

        await wm.Received(1).MoveWindowToWorkspaceByAddressAsync("0x123", "5", Arg.Any<CancellationToken>());
    }

    // ---- variable resolution ----------------------------------------------------------

    [Fact]
    public async Task ExecuteStepAsync_WhenStepUsesVariable_ResolvesBeforeExecution()
    {
        var wm = FakeWindowManager(null);
        wm.FocusWindowByAddressAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        var vars = Vars();
        vars["addr"] = "0xBEEF";

        await Executor(wm).ExecuteStepAsync("window focus address $addr", 1, vars, CancellationToken.None);

        await wm.Received(1).FocusWindowByAddressAsync("0xBEEF", Arg.Any<CancellationToken>());
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
        RunScriptWindowExecutor.IsWindowStep(step).Should().Be(expected);
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
    [InlineData("window unknown x", "Unknown window sub-command")]
    public void Validate_ReturnsExpectedError(string step, string? errorFragment)
    {
        var result = RunScriptWindowExecutor.Validate(step);

        if (errorFragment == null)
            result.Should().BeNull();
        else
            result.Should().Contain(errorFragment);
    }

    // ---- helpers ----------------------------------------------------------------------

    private static RunScriptWindowExecutor Executor(IWindowManager wm) => new(wm);

    private static Dictionary<string, string> Vars() =>
        new(System.StringComparer.OrdinalIgnoreCase);

    private static IWindowManager FakeWindowManager(WindowInfo? activeWindow)
    {
        var wm = Substitute.For<IWindowManager>();
        wm.GetActiveWindowAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<WindowInfo?>(activeWindow));
        return wm;
    }

    private static IWindowManager FakeWindowList(params WindowInfo[] windows)
    {
        var wm = Substitute.For<IWindowManager>();
        wm.GetWindowsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<System.Collections.Generic.IReadOnlyList<WindowInfo>>(windows));
        return wm;
    }
}
