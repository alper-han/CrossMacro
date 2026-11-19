using Avalonia.Input;
using CrossMacro.UI.Controls;
namespace CrossMacro.UI.Tests.Controls;

[Collection(TestCollections.HeadlessStartup)]
public sealed class HotkeyCaptureTests
{
    [Fact]
    public Task InvalidCapturedChord_SetsErrorWithoutRaisingHotkeyChanged() =>
        HeadlessTestRunner.RunAsync(async token =>
        {
            var hotkeys = Substitute.For<IGlobalHotkeyService>();
            hotkeys.CaptureNextKeyAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult("bad"));
            var capture = new HotkeyCapture
            {
                Hotkey = "F9",
                GlobalHotkeyService = hotkeys,
                ValidationFunc = _ => (false, "bad capture"),
            };
            var changed = 0;
            capture.HotkeyChanged += (_, _) => changed++;
            var window = new Window { Content = capture };
            window.Show();

            try
            {
                var border = capture.FindControl<Border>("HotkeyBorder");
                Assert.NotNull(border);
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => { });
                var pointer = Substitute.For<IPointer>();
                _ = pointer.Captured.Returns((IInputElement?)null);
                border.RaiseEvent(new PointerPressedEventArgs(border, pointer, window, default, 0, default, KeyModifiers.None, 1));
                await hotkeys.Received(1).CaptureNextKeyAsync(Arg.Any<CancellationToken>());
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => { });
                Assert.False(capture.IsValid);
                Assert.Equal("bad capture", capture.ErrorMessage);
                Assert.Equal("F9", capture.Hotkey);
                Assert.Equal(0, changed);
            }
            finally
            {
                window.Close();
            }
        });

    [Fact]
    public Task ValidCapturedChord_UpdatesHotkeyAndClearsCapturingState() =>
        HeadlessTestRunner.RunAsync(async token =>
        {
            var hotkeys = Substitute.For<IGlobalHotkeyService>();
            hotkeys.CaptureNextKeyAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult("Ctrl+F10"));
            var capture = new HotkeyCapture { Hotkey = "F9", GlobalHotkeyService = hotkeys };
            string? changedTo = null;
            capture.HotkeyChanged += (_, hotkey) => changedTo = hotkey;
            var window = new Window { Content = capture };
            window.Show();

            try
            {
                await PressAsync(capture, window);

                Assert.Equal("Ctrl+F10", changedTo);
                Assert.Equal("Ctrl+F10", capture.Hotkey);
                Assert.Equal("Ctrl+F10", capture.DisplayString);
                Assert.False(capture.IsCapturing);
                Assert.True(capture.IsValid);
            }
            finally
            {
                window.Close();
            }
        });

    [Fact]
    public Task FailedCapture_LeavesHotkeyUnchangedAndStopsCapturing() =>
        HeadlessTestRunner.RunAsync(async token =>
        {
            var hotkeys = Substitute.For<IGlobalHotkeyService>();
            hotkeys.CaptureNextKeyAsync(Arg.Any<CancellationToken>()).Returns(Task.FromException<string>(new InvalidOperationException("device lost")));
            var capture = new HotkeyCapture { Hotkey = "F9", GlobalHotkeyService = hotkeys };
            var window = new Window { Content = capture };
            window.Show();

            try
            {
                await PressAsync(capture, window);

                Assert.Equal("F9", capture.Hotkey);
                Assert.False(capture.IsCapturing);
            }
            finally
            {
                window.Close();
            }
        });

    private static async Task PressAsync(HotkeyCapture capture, Window window)
    {
        var border = capture.FindControl<Border>("HotkeyBorder");
        Assert.NotNull(border);
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => { });
        var pointer = Substitute.For<IPointer>();
        _ = pointer.Captured.Returns((IInputElement?)null);
        border.RaiseEvent(new PointerPressedEventArgs(border, pointer, window, default, 0, default, KeyModifiers.None, 1));
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => { });
    }
}
