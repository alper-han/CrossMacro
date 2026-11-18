using System.Globalization;

namespace CrossMacro.Infrastructure.Tests.Services.TextExpansion;

internal class RecordingInputSimulator(List<string> events) : TestInputSimulator
{
    public override void KeyPress(int keyCode, bool pressed)
    {
        if (pressed)
        {
            events.Add(string.Create(CultureInfo.InvariantCulture, $"input:key:{keyCode}"));
        }

        base.KeyPress(keyCode, pressed);
    }
}
