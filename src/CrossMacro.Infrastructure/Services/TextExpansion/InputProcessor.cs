using System;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Core.Services.TextExpansion;
using CrossMacro.Core.Logging;
using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Infrastructure.Services.TextExpansion
{
    public class InputProcessor : IInputProcessor
    {
        private readonly IKeyboardLayoutService _layoutService;
        private readonly Lock _stateLock = new();
        
        // Modifier state
        private bool _isLeftShiftPressed;
        private bool _isRightShiftPressed;
        private bool _isRightAltPressed; // AltGr
        private bool _isLeftAltPressed;
        private bool _isLeftCtrlPressed;
        private bool _isRightCtrlPressed; // Not used for char mapping directly usually, but good to track
        private bool _isCapsLockOn;
        private bool _isAltGrPressed; // Computed
        private readonly HashSet<int> _pressedKeys = new();

        // Debouncing state
        private int _lastKey;
        private long _lastPressTime;
        private const long DebounceTicks = 20 * 10000; // 20ms in ticks

        public event Action<char>? CharacterReceived;
        public event Action<int>? SpecialKeyReceived;

        public bool AreModifiersPressed
        {
            get
            {
                lock (_stateLock)
                {
                    return _isLeftShiftPressed || _isRightShiftPressed ||
                        _isLeftAltPressed || _isRightAltPressed ||
                        _isLeftCtrlPressed || _isRightCtrlPressed;
                }
            }
        }

        public bool IsKeyPressed(int keyCode)
        {
            lock (_stateLock)
            {
                return _pressedKeys.Contains(keyCode);
            }
        }

        public InputProcessor(IKeyboardLayoutService layoutService)
        {
            _layoutService = layoutService;
        }

        public void ProcessEvent(InputCaptureEventArgs e)
        {
            // Only process key events
            if (e.Type != InputEventType.Key) return;

            char? receivedCharacter = null;
            int? receivedSpecialKey = null;

            lock (_stateLock)
            {
                // Update Modifier State
                UpdateModifiers(e);
                UpdatePressedKeys(e);

                // Toggle CapsLock on Press
                if (e.Code == InputEventCode.KEY_CAPSLOCK && e.Value == 1)
                {
                    _isCapsLockOn = !_isCapsLockOn;
                    return;
                }

                // Only process key PRESS (value == 1) for actual typing logic
                if (e.Value != 1) return;

                // Debouncing check
                long now = DateTime.UtcNow.Ticks;
                if (e.Code == _lastKey && (now - _lastPressTime) < DebounceTicks)
                {
                    return;
                }
                _lastKey = e.Code;
                _lastPressTime = now;

                // Check for Special Keys first
                if (e.Code == InputEventCode.KEY_BACKSPACE)
                {
                    receivedSpecialKey = e.Code;
                }
                else if (e.Code == InputEventCode.KEY_ENTER)
                {
                    receivedSpecialKey = e.Code;
                    // Enter might also produce a char (newline), but usually clears buffer
                }
                else
                {
                    // Map key to char
                    receivedCharacter = _layoutService.GetCharFromKeyCode(e.Code,
                        _isLeftShiftPressed, _isRightShiftPressed,
                        _isRightAltPressed, _isLeftAltPressed, _isLeftCtrlPressed, _isCapsLockOn);

                    if (!receivedCharacter.HasValue && e.Code == InputEventCode.KEY_SPACE)
                    {
                        // Explicitly handle space if layout service returns null for it (it shouldn't, but safe fallback)
                        receivedCharacter = _layoutService.GetCharFromKeyCode(
                            InputEventCode.KEY_SPACE,
                            false,
                            false,
                            false,
                            false,
                            false,
                            false);
                    }
                }
            }

            if (receivedSpecialKey.HasValue)
            {
                SpecialKeyReceived?.Invoke(receivedSpecialKey.Value);
            }
            else if (receivedCharacter.HasValue)
            {
                CharacterReceived?.Invoke(receivedCharacter.Value);
            }
        }

        private void UpdateModifiers(InputCaptureEventArgs e)
        {
            // Value 1 = Press, 2 = Repeat, 0 = Release.
            // We consider it pressed if 1 or 2.
            bool isPressed = e.Value == 1 || e.Value == 2;

            switch (e.Code)
            {
                case InputEventCode.KEY_LEFTSHIFT:
                    _isLeftShiftPressed = isPressed;
                    break;
                case InputEventCode.KEY_RIGHTSHIFT:
                    _isRightShiftPressed = isPressed;
                    break;
                case InputEventCode.KEY_RIGHTALT:
                    _isRightAltPressed = isPressed;
                    _isAltGrPressed = _isRightAltPressed; // Treat Right Alt as AltGr
                    break;
                case InputEventCode.KEY_LEFTALT:
                    _isLeftAltPressed = isPressed;
                    break;
                case InputEventCode.KEY_LEFTCTRL:
                    _isLeftCtrlPressed = isPressed;
                    break;
                case InputEventCode.KEY_RIGHTCTRL:
                    _isRightCtrlPressed = isPressed;
                    break;
            }
        }

        private void UpdatePressedKeys(InputCaptureEventArgs e)
        {
            if (e.Value == 0)
            {
                _pressedKeys.Remove(e.Code);
            }
            else if (e.Value == 1 || e.Value == 2)
            {
                _pressedKeys.Add(e.Code);
            }
        }

        public void Reset()
        {
            lock (_stateLock)
            {
                _isLeftShiftPressed = false;
                _isRightShiftPressed = false;
                _isRightAltPressed = false;
                _isLeftAltPressed = false;
                _isLeftCtrlPressed = false;
                _isRightCtrlPressed = false;
                _isAltGrPressed = false;
                _pressedKeys.Clear();
                // _isCapsLockOn ? Usually persistent, don't reset caps lock
                _lastKey = 0;
                _lastPressTime = 0;
            }
        }
    }
}
