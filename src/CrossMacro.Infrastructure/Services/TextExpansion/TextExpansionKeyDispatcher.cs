using System;
using System.Threading.Tasks;
using CrossMacro.Core.Services;

namespace CrossMacro.Infrastructure.Services.TextExpansion;

internal sealed class TextExpansionKeyDispatcher
{
    public async Task SendKeyAsync(
        IInputSimulator simulator,
        int keyCode,
        bool shift = false,
        bool altGr = false,
        bool ctrl = false)
    {
        ArgumentNullException.ThrowIfNull(simulator);

        if (ctrl)
        {
            SendKeyState(simulator, InputEventCode.KEY_LEFTCTRL, true);
        }

        if (shift)
        {
            SendKeyState(simulator, InputEventCode.KEY_LEFTSHIFT, true);
        }

        if (altGr)
        {
            SendKeyState(simulator, InputEventCode.KEY_RIGHTALT, true);
        }

        SendKeyState(simulator, keyCode, true);
        await Task.Delay(TextExpansionExecutionTimings.KeyPressReleaseDelay);
        SendKeyState(simulator, keyCode, false);

        if (altGr)
        {
            SendKeyState(simulator, InputEventCode.KEY_RIGHTALT, false);
        }

        if (shift)
        {
            SendKeyState(simulator, InputEventCode.KEY_LEFTSHIFT, false);
        }

        if (ctrl)
        {
            SendKeyState(simulator, InputEventCode.KEY_LEFTCTRL, false);
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
