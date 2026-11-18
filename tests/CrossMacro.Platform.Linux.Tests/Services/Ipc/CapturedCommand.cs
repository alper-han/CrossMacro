namespace CrossMacro.Platform.Linux.Tests.Services.Ipc;

/// <summary>A command <see cref="TestIpcDaemon"/> received from the client.</summary>
internal readonly record struct CapturedCommand(
    IpcOpCode OpCode,
    int RequestId = 0,
    bool CaptureMouse = false,
    bool CaptureKeyboard = false,
    ushort Type = 0,
    ushort Code = 0,
    int Value = 0,
    long DelayAfterMicroseconds = 0,
    int Width = 0,
    int Height = 0);
