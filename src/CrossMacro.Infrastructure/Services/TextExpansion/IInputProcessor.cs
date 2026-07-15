
namespace CrossMacro.Infrastructure.Services.TextExpansion;

public interface IInputProcessor
{
    public bool AreModifiersPressed { get; }

    public bool IsKeyPressed(int keyCode);

    public bool IsSuspended { get; }

    public event Action<char> CharacterReceived;

    public event Action<int> SpecialKeyReceived;

    public void ProcessEvent(CapturedInputEvent e);

    public void Reset();

    public void Suspend();

    public void ResumeInputProcessing();
}
