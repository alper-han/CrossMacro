using CrossMacro.Commands.WindowManagement;
using CrossMacro.Platform.Abstractions.WindowManagement;
using Microsoft.Extensions.Time.Testing;

namespace CrossMacro.Commands.Tests;

public sealed class WindowCommandServiceTests
{
    private static readonly WindowInfo Browser = new() { Address = "0x1", Title = "Docs - Firefox", Class = "firefox" };
    private static readonly WindowInfo Editor = new() { Address = "0x2", Title = "Editor", Class = "Code" };

    private readonly IWindowManager _manager = Substitute.For<IWindowManager>();

    public WindowCommandServiceTests()
    {
        _ = _manager.IsSupported.Returns(returnThis: true);
        _ = _manager.GetWindowsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<WindowInfo>>([Browser, Editor]));
    }

    [Fact]
    public async Task Search_MatchesTheSelectorBySubstring()
    {
        var result = await new WindowCommandService(_manager).ExecuteAsync(
            new WindowOptions(WindowAction.Search, new WindowSelector(WindowSelectorKind.Title, "fire")),
            CancellationToken.None);

        Assert.True(result.Success);
        var data = Assert.IsType<WindowListData>(result.Data);
        Assert.Equal("0x1", Assert.Single(data.Windows).Address);
    }

    [Fact]
    public async Task Focus_ByClass_UsesTheClassSubstringOperation()
    {
        _ = _manager.FocusWindowByClassAsync("code", Arg.Any<CancellationToken>()).Returns(returnThis: true);

        var result = await new WindowCommandService(_manager).ExecuteAsync(
            new WindowOptions(WindowAction.Focus, new WindowSelector(WindowSelectorKind.Class, "code")),
            CancellationToken.None);

        Assert.True(result.Success);
        _ = await _manager.Received(1).FocusWindowByClassAsync("code", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Focus_WhenTheManagerRefuses_ReportsAFailedMutation()
    {
        _ = _manager.FocusWindowByClassAsync("code", Arg.Any<CancellationToken>()).Returns(returnThis: false);

        var result = await new WindowCommandService(_manager).ExecuteAsync(
            new WindowOptions(WindowAction.Focus, new WindowSelector(WindowSelectorKind.Class, "code")),
            CancellationToken.None);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task List_WhenThereIsNoWindowManager_ReturnsEnvironmentError()
    {
        var result = await new WindowCommandService(new NullWindowManager()).ExecuteAsync(new WindowOptions(WindowAction.List), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ExecutionOutcomeCode.EnvironmentError, result.Outcome);
    }

    [Fact]
    public async Task InvalidActionOptions_ReturnInvalidArguments()
    {
        var service = new WindowCommandService(_manager);

        var missingSelector = await service.ExecuteAsync(new WindowOptions(WindowAction.Search), CancellationToken.None);
        var missingCoordinates = await service.ExecuteAsync(new WindowOptions(WindowAction.Move, X: 1), CancellationToken.None);
        var unsupportedCloseSelector = await service.ExecuteAsync(new WindowOptions(WindowAction.Close, new WindowSelector(WindowSelectorKind.Class, "code")), CancellationToken.None);

        foreach (var result in new[] { missingSelector, missingCoordinates, unsupportedCloseSelector })
        {
            Assert.False(result.Success);
            Assert.Equal(ExecutionOutcomeCode.InvalidArguments, result.Outcome);
        }
    }

    [Fact]
    public async Task Wait_PollsOnTheInjectedTimeProviderUntilTheWindowAppears()
    {
        _ = _manager.GetWindowsAsync(Arg.Any<CancellationToken>()).Returns(
            _ => Task.FromResult<IReadOnlyList<WindowInfo>>([]),
            _ => Task.FromResult<IReadOnlyList<WindowInfo>>([Editor]));
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var service = new WindowCommandService(_manager, timeProvider);

        var waitTask = service.ExecuteAsync(
            new WindowOptions(WindowAction.Wait, new WindowSelector(WindowSelectorKind.Title, "Editor"), TimeoutMs: 600_000),
            CancellationToken.None);
        using var guard = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!waitTask.IsCompleted)
        {
            guard.Token.ThrowIfCancellationRequested();
            timeProvider.Advance(TimeSpan.FromMilliseconds(200));
            await Task.Yield();
        }

        var result = await waitTask;

        Assert.True(result.Success);
        Assert.True(Assert.IsType<WindowWaitData>(result.Data).Found);
    }
}
