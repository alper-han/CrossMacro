using System;

namespace CrossMacro.Core.Services.Playback;

/// <summary>
/// Interface for executing input events (mouse, keyboard)
/// </summary>
public interface IEventExecutor : IDisposable
{
    /// <summary>
    /// Initialize the executor (create virtual device)
    /// </summary>
    void Initialize(int screenWidth, int screenHeight);
    
    /// <summary>
    /// Move mouse to absolute position
    /// </summary>
    void MoveAbsolute(int x, int y);
    
    /// <summary>
    /// Move mouse by relative delta
    /// </summary>
    void MoveRelative(int dx, int dy);
    
    /// <summary>
    /// Press or release mouse button
    /// </summary>
    void EmitButton(ushort button, bool pressed);
    
    /// <summary>
    /// Emit mouse scroll
    /// </summary>
    void EmitScroll(int value);
    
    /// <summary>
    /// Press or release keyboard key
    /// </summary>
    void EmitKey(int keyCode, bool pressed);
    
    /// <summary>
    /// Release all pressed inputs (safety)
    /// </summary>
    void ReleaseAll();
    
    /// <summary>
    /// Whether any mouse button is currently pressed
    /// </summary>
    bool IsMouseButtonPressed { get; }
}
