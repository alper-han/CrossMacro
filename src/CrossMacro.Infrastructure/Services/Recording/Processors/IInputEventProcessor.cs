
namespace CrossMacro.Infrastructure.Services.Recording.Processors;

public interface IInputEventProcessor
{
    void Configure(bool recordMouse, bool recordKeyboard, HashSet<int>? ignoredKeys, bool isAbsoluteCoordinates = false);

    MacroEvent? Process(InputCaptureEventArgs args, long timestamp);
}
