
namespace CrossMacro.Infrastructure.Services.Recording.Processors;

public interface IInputEventProcessor
{
    public void Configure(bool recordMouse, bool recordKeyboard, IReadOnlySet<int>? ignoredKeys, bool isAbsoluteCoordinates = false);

    public MacroEvent? Process(CapturedInputEvent args, long timestamp);

    public MacroEvent? ProcessPositionSample(
        CoordinateSample sample,
        long timestamp,
        CoordinateSampleSpace? coordinateSpace = null);
}
