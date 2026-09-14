using System.Globalization;

namespace CrossMacro.Daemon.Contracts.Ipc;

/// <summary>
/// Owns v4 message field ordering and wire-level bounds. Callers own opcode
/// dispatch, authorization, transport serialization and flush timing.
/// </summary>
public static class IpcMessageCodec
{
    public static void WriteResolutionPayload(BinaryWriter writer, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.Write(width);
        writer.Write(height);
    }

    public static (int Width, int Height) ReadResolutionPayload(BinaryReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        return (reader.ReadInt32(), reader.ReadInt32());
    }

    public static (ushort Type, ushort Code, int Value) ReadSingleSimulationPayload(BinaryReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        return (reader.ReadUInt16(), reader.ReadUInt16(), reader.ReadInt32());
    }

    public static int ReadRequestId(BinaryReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        return reader.ReadInt32();
    }

    public static (int RequestId, string Message) ReadRequestFailurePayload(BinaryReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        return (reader.ReadInt32(), reader.ReadString());
    }

    public static (int RequestId, int EventCount) ReadSimulationBatchCompletedPayload(BinaryReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        return (ReadRequestId(reader), reader.ReadInt32());
    }

    public static void WriteError(BinaryWriter writer, string message)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.Write((byte)IpcOpCode.Error);
        writer.Write(message);
    }

    public static void WriteCaptureStartPayload(BinaryWriter writer, int requestId, bool mouse, bool keyboard)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.Write(requestId);
        writer.Write(mouse);
        writer.Write(keyboard);
    }

    public static (int RequestId, bool Mouse, bool Keyboard) ReadCaptureStartPayload(BinaryReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        return (reader.ReadInt32(), reader.ReadBoolean(), reader.ReadBoolean());
    }

    public static void WriteInputEvent(BinaryWriter writer, IpcInputEvent inputEvent)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.Write((byte)IpcOpCode.InputEvent);
        writer.Write(inputEvent.Type);
        writer.Write(inputEvent.Code);
        writer.Write(inputEvent.Value);
        writer.Write(inputEvent.Timestamp);
    }

    public static IpcInputEvent ReadInputEventPayload(BinaryReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        return new IpcInputEvent
        {
            Type = reader.ReadByte(), Code = reader.ReadInt32(),
            Value = reader.ReadInt32(), Timestamp = reader.ReadInt64(),
        };
    }

    public static void WriteSimulationBatchHeader(BinaryWriter writer, int requestId, int eventCount)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.Write((byte)IpcOpCode.SimulateEventBatch);
        writer.Write(requestId);
        writer.Write(eventCount);
    }

    public static void WriteSimulationEvent(BinaryWriter writer, IpcSimulationRequest inputEvent)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.Write(inputEvent.Type);
        writer.Write(inputEvent.Code);
        writer.Write(inputEvent.Value);
        writer.Write(inputEvent.DelayAfterMicroseconds);
    }

    public static IpcSimulationRequest[] ReadSimulationBatchPayload(BinaryReader reader)
    {
        var result = ReadSimulationBatch(reader);
        if (!result.Success)
        {
            throw new InvalidDataException(result.ErrorMessage);
        }

        return result.GetDecodedEvents();
    }

    public static IpcSimulationBatchReadResult ReadSimulationBatch(BinaryReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        var eventCount = reader.ReadInt32();
        if (eventCount is <= 0 or > IpcProtocol.MaxSimulationBatchEvents)
        {
            return new IpcSimulationBatchReadResult([], string.Create(CultureInfo.InvariantCulture,
                $"Simulation batch event count {eventCount} is outside the allowed range 1-{IpcProtocol.MaxSimulationBatchEvents}."),
                hasCompleteFrame: eventCount is 0);
        }

        var events = new IpcSimulationRequest[eventCount];
        for (var index = 0; index < events.Length; index++)
        {
            events[index] = new IpcSimulationRequest
            {
                Type = reader.ReadUInt16(), Code = reader.ReadUInt16(),
                Value = reader.ReadInt32(), DelayAfterMicroseconds = reader.ReadInt64(),
            };
        }

        // Consume the entire bounded frame before semantic validation. Otherwise a
        // later event's type byte could be mistaken for the next command opcode.
        var errorMessage = ValidateSimulationBatch(events);
        return new IpcSimulationBatchReadResult(errorMessage is null ? events : [], errorMessage, hasCompleteFrame: true);
    }

    private static string? ValidateSimulationBatch(ReadOnlySpan<IpcSimulationRequest> events)
    {
        long totalDelayMicroseconds = 0;
        foreach (var inputEvent in events)
        {
            if (inputEvent.DelayAfterMicroseconds is < 0 or > IpcProtocol.MaxSimulationBatchDelayMicroseconds)
            {
                return string.Create(CultureInfo.InvariantCulture,
                    $"Simulation batch delay {inputEvent.DelayAfterMicroseconds}us is outside the allowed range 0-{IpcProtocol.MaxSimulationBatchDelayMicroseconds}us.");
            }

            totalDelayMicroseconds += inputEvent.DelayAfterMicroseconds;
            if (totalDelayMicroseconds > IpcProtocol.MaxSimulationBatchTotalDelayMicroseconds)
            {
                return string.Create(CultureInfo.InvariantCulture,
                    $"Simulation batch total delay {totalDelayMicroseconds}us exceeds the allowed maximum of {IpcProtocol.MaxSimulationBatchTotalDelayMicroseconds}us.");
            }
        }
        return null;
    }

    public static void WriteCaptureStarted(BinaryWriter writer, int requestId)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.Write((byte)IpcOpCode.CaptureStarted);
        writer.Write(requestId);
    }

    public static void WriteSimulationBatchCompleted(BinaryWriter writer, int requestId, int eventCount)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.Write((byte)IpcOpCode.SimulationBatchCompleted);
        writer.Write(requestId);
        writer.Write(eventCount);
    }

    public static void WriteRequestFailure(BinaryWriter writer, IpcOpCode opcode, int requestId, string message)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.Write((byte)opcode);
        writer.Write(requestId);
        writer.Write(message);
    }
}
