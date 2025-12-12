using System;

namespace CrossMacro.Core.Services;

public interface IInputSimulator : IDisposable
{
    string ProviderName { get; }
    
    bool IsSupported { get; }
    
    void Initialize(int screenWidth = 0, int screenHeight = 0);
    
    void MoveAbsolute(int x, int y);
    
    void MoveRelative(int dx, int dy);
    
    void MouseButton(int button, bool pressed);
    
    void Scroll(int delta);
    
    void KeyPress(int keyCode, bool pressed);
    
    void Sync();
}
