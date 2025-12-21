namespace CrossMacro.Core.Ipc;

public enum IpcOpCode : byte
{
    /// <summary>
    /// Initial handshake to verify version.
    /// Payload: int (Protocol Version)
    /// </summary>
    Handshake = 0x01,

    /// <summary>
    /// Request to start capturing input events.
    /// Payload: bool (Capture Mouse), bool (Capture Keyboard)
    /// </summary>
    StartCapture = 0x02,

    /// <summary>
    /// Request to stop capturing input events.
    /// Payload: None
    /// </summary>
    StopCapture = 0x03,

    /// <summary>
    /// Input event sent from Daemon to Client.
    /// Payload: InputEventType (byte), int (Code), int (Value), long (Timestamp)
    /// </summary>
    InputEvent = 0x04,

    /// <summary>
    /// Request to simulate an input event (Client to Daemon).
    /// Payload: ushort (Type), ushort (Code), int (Value)
    /// </summary>
    SimulateEvent = 0x05,

    /// <summary>
    /// Error report.
    /// Payload: string (Message)
    /// </summary>
    Error = 0xFF,
    
    /// <summary>
    /// Configure virtual device resolution.
    /// Payload: int (Width), int (Height)
    /// </summary>
    ConfigureResolution = 0x06
}
