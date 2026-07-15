
namespace CrossMacro.Infrastructure.Services.Recording.Processors;

public interface IInputEventProcessor
{
    public void Configure(bool recordMouse, bool recordKeyboard, HashSet<int>? ignoredKeys, bool isAbsoluteCoordinates = false);

    public MacroEvent? Process(CapturedInputEvent args, long timestamp);
}
