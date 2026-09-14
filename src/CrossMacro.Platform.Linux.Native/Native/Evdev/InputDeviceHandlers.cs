namespace CrossMacro.Platform.Linux.Native.Evdev;

[Flags]
internal enum InputDeviceHandlers
{
    None = 0,
    Mouse = 1,
    Keyboard = 2,
}
