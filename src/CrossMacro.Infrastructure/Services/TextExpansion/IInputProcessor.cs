using System;
using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Infrastructure.Services.TextExpansion;

public interface IInputProcessor
{
    bool AreModifiersPressed { get; }

    bool IsKeyPressed(int keyCode);

    event Action<char> CharacterReceived;

    event Action<int> SpecialKeyReceived;

    void ProcessEvent(InputCaptureEventArgs e);

    void Reset();
}
