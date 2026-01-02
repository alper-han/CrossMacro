using CrossMacro.Core.Services;
using CrossMacro.Platform.Linux.Services.Keyboard;

namespace CrossMacro.Platform.Linux.Services;

/// <summary>
/// Linux keyboard layout service - Facade coordinating layout detection, keycode mapping, and XKB state.
/// Implements IKeyboardLayoutService by delegating to specialized components following SRP.
/// </summary>
public class LinuxKeyboardLayoutService : IKeyboardLayoutService, IDisposable
{
    private readonly ILinuxKeyCodeMapper _keyCodeMapper;
    private readonly IXkbStateManager _xkbState;

    public LinuxKeyboardLayoutService(
        ILinuxLayoutDetector layoutDetector,
        ILinuxKeyCodeMapper keyCodeMapper,
        IXkbStateManager xkbState)
    {
        _keyCodeMapper = keyCodeMapper;
        _xkbState = xkbState;
        
        // Detect layout and initialize XKB
        var layout = layoutDetector.DetectLayout();
        _xkbState.Initialize(layout);
    }

    /// <inheritdoc />
    public string GetKeyName(int keyCode) => _keyCodeMapper.GetKeyName(keyCode);

    /// <inheritdoc />
    public int GetKeyCode(string keyName) => _keyCodeMapper.GetKeyCode(keyName);

    /// <inheritdoc />
    public char? GetCharFromKeyCode(int keyCode, bool leftShift, bool rightShift, bool rightAlt, bool leftAlt, bool leftCtrl, bool capsLock)
    {
        bool shift = leftShift || rightShift;
        bool altGr = rightAlt;
        return _xkbState.GetCharFromKeyCode(keyCode, shift, altGr, capsLock);
    }

    /// <inheritdoc />
    public (int KeyCode, bool Shift, bool AltGr)? GetInputForChar(char c) => _xkbState.GetInputForChar(c);

    public void Dispose()
    {
        _xkbState.Dispose();
        GC.SuppressFinalize(this);
    }
}
