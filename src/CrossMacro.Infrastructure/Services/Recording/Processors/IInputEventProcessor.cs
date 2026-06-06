using System.Collections.Generic;
using CrossMacro.Core.Models;
using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Infrastructure.Services.Recording.Processors;

public interface IInputEventProcessor
{
    void Configure(bool recordMouse, bool recordKeyboard, bool recordGamepad, HashSet<int>? ignoredKeys, bool isAbsoluteCoordinates = false);

    MacroEvent? Process(InputCaptureEventArgs args, long timestamp);
}
