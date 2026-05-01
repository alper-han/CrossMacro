using System.IO;
using CrossMacro.Infrastructure.Linux.Native.UInput;

namespace CrossMacro.Daemon.Services;

internal sealed class DaemonProtocolSession
{
    private readonly DaemonInputEventEncoder _inputEventEncoder;

    public DaemonProtocolSession(
        BinaryReader reader,
        BinaryWriter writer,
        Stream stream,
        int maxBufferedCaptureEvents,
        DaemonInputEventEncoder inputEventEncoder)
    {
        Reader = reader;
        Writer = writer;
        Stream = stream;
        WriterGate = new OrderedWriteGate();
        CaptureForwarding = new CaptureForwardingCoordinator(maxBufferedCaptureEvents);
        _inputEventEncoder = inputEventEncoder;
    }

    public BinaryReader Reader { get; }

    public BinaryWriter Writer { get; }

    public Stream Stream { get; }

    public OrderedWriteGate WriterGate { get; }

    public CaptureForwardingCoordinator CaptureForwarding { get; }

    public bool Disconnected { get; private set; }

    public void WriteInputEvent(UInputNative.input_event inputEvent)
    {
        _inputEventEncoder.Write(Writer, inputEvent);
    }

    public void MarkDisconnected()
    {
        Disconnected = true;
    }
}
