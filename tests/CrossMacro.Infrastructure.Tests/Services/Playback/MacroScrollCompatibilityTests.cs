namespace CrossMacro.Infrastructure.Tests.Services.Playback;

/// <summary>
/// Every released CrossMacro version stores a recorded wheel event as a direction-only
/// button event (<c>C,x,y,ScrollUp</c>); the platform wheel magnitude (Windows ±120
/// multiples, macOS trackpad pixels) is never written. Playback therefore always asks
/// the simulator for exactly one notch, whichever platform wrote the file.
/// </summary>
public sealed class MacroScrollCompatibilityTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"crossmacro_scroll_{Guid.NewGuid():N}");

    public MacroScrollCompatibilityTests() => _ = Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Theory]
    [InlineData("", "C,100,200,ScrollUp", 1, false)]
    [InlineData("", "C,100,200,ScrollDown", -1, false)]
    [InlineData("# Format: CrossMacroFormatV2\n", "C,640,360,ScrollUp", 1, false)]
    [InlineData("# Format: CrossMacroFormatV2\n", "C,640,360,ScrollDown", -1, false)]
    [InlineData("# Format: CrossMacroFormatV3\n", "C,640,360,ScrollRight", 1, true)]
    [InlineData("# Format: CrossMacroFormatV3\n", "C,640,360,ScrollLeft", -1, true)]
    public async Task LegacyRecording_ReplaysAsSingleNotch(string formatHeader, string eventLine, int expectedNotches, bool expectedHorizontal)
    {
        var path = Path.Combine(_directory, $"{Guid.NewGuid():N}.macro");
        await File.WriteAllTextAsync(
            path,
            $"# Name: Legacy scroll\n# Created: 2025-01-01T00:00:00Z\n# DurationMs: 0\n# IsAbsolute: True\n{formatHeader}[Events]\nW,10\n{eventLine}\n",
            TestContext.Current.CancellationToken);
        var manager = new MacroFileManager(() => Substitute.For<IKeyCodeMapper>());
        var simulator = Substitute.For<IInputSimulator>();
        using var executor = new MacroEventExecutor(
            simulator,
            Substitute.For<IButtonStateTracker>(),
            Substitute.For<IKeyStateTracker>(),
            Substitute.For<IPlaybackMouseButtonMapper>(),
            Substitute.For<IPlaybackCoordinator>());
        executor.Initialize(1920, 1080);

        var macro = await manager.LoadAsync(path);
        _ = macro.Should().NotBeNull();
        var scroll = macro.Events.Should().ContainSingle().Subject;
        executor.Execute(scroll, coordinateMode: null);

        simulator.Received(1).Scroll(expectedNotches, expectedHorizontal);
    }
}
