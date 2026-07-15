namespace CrossMacro.Platform.Linux.Ipc;

internal readonly record struct CaptureCommand(
    CaptureCommandType Type,
    bool CaptureMouse = false,
    bool CaptureKeyboard = false);
