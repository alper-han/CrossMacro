namespace CrossMacro.Platform.Linux.Tests.Services.Ipc;

/// <summary>How <see cref="TestIpcDaemon"/> answers the handshake and later commands.</summary>
internal enum HandshakeBehavior
{
    Success = 0,
    ErrorResponse = 1,
    ProtocolMismatch = 2,
    NoResponse = 3,
    FailSecondStartAfterDelay = 4,
    DelayAllCaptureStartAcks = 5,
    FailFirstStartAfterDelay = 6,
    FailSimulationBatch = 7,
    DropSimulationBatchBeforeAck = 8,
    HoldSimulationBatchWithoutAck = 9,
    CorruptSimulationBatchCount = 10,
    StopReadingAfterHandshake = 11,
}
