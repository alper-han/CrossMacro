using Avalonia.Input;
using CrossMacro.UI.Controls;
namespace CrossMacro.UI.Tests.Controls;

[Collection("Headless startup")]
public sealed class HotkeyCaptureTests
{
    [Fact]
    public void ImplementsDisposableOwnershipContract()
    {
        Assert.Contains(typeof(IDisposable), typeof(HotkeyCapture).GetInterfaces());
    }
    [Fact]
    public Task InvalidCapturedChord_SetsErrorWithoutRaisingHotkeyChanged() =>
        CrossMacro.UI.Tests.Services.DesktopQuickSetupDialogTests.RunHeadlessAsync(async token =>
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
                capture.Dispose();
            }
        });
}
