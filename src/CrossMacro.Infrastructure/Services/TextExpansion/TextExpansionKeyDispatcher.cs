
namespace CrossMacro.Infrastructure.Services.TextExpansion;

internal sealed class TextExpansionKeyDispatcher
{
    public async Task SendKeyAsync(
        IInputSimulator simulator,
        int keyCode,
        bool shift = false,
        bool altGr = false,
        bool ctrl = false,
        bool meta = false)
    {
        ArgumentNullException.ThrowIfNull(simulator);

        if (ctrl)
        {
            SendKeyState(simulator, InputEventCode.KEY_LEFTCTRL, pressed: true);
        }

        if (meta)
        {
            SendKeyState(simulator, InputEventCode.KEY_LEFTMETA, pressed: true);
        }

        if (shift)
        {
            SendKeyState(simulator, InputEventCode.KEY_LEFTSHIFT, pressed: true);
        }

        if (altGr)
        {
            SendKeyState(simulator, InputEventCode.KEY_RIGHTALT, pressed: true);
        }

        SendKeyState(simulator, keyCode, pressed: true);
        await Task.Delay(TextExpansionExecutionTimings.KeyPressReleaseDelay);
        SendKeyState(simulator, keyCode, pressed: false);

        if (altGr)
        {
            SendKeyState(simulator, InputEventCode.KEY_RIGHTALT, pressed: false);
        }

        if (shift)
        {
            SendKeyState(simulator, InputEventCode.KEY_LEFTSHIFT, pressed: false);
        }

        if (meta)
        {
            SendKeyState(simulator, InputEventCode.KEY_LEFTMETA, pressed: false);
        }

        if (ctrl)
        {
            SendKeyState(simulator, InputEventCode.KEY_LEFTCTRL, pressed: false);
        }
    }

    private static void SendKeyState(IInputSimulator simulator, int keyCode, bool pressed)
    {
        SendMarkedKeyPress(simulator, keyCode, pressed);
        simulator.Sync();
    }

    private static void SendMarkedKeyPress(IInputSimulator simulator, int keyCode, bool pressed)
    {
        if (simulator is ITaggedKeyboardInputSimulator taggedKeyboardInputSimulator &&
            taggedKeyboardInputSimulator.SupportsTaggedKeyboardInput)
        {
            taggedKeyboardInputSimulator.KeyPressTagged(
                keyCode,
                pressed,
                InputEventMarkers.TextExpansionKeyboardEvent);
            return;
        }

        simulator.KeyPress(keyCode, pressed);
    }
}
