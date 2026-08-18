
namespace CrossMacro.Daemon.Services;

internal sealed class DaemonProtocolSession(
    BinaryReader reader,
    BinaryWriter writer,
    Stream stream,
    int maxBufferedCaptureEvents)
{
    public BinaryReader Reader { get; } = reader;

    public BinaryWriter Writer { get; } = writer;

    public Stream Stream { get; } = stream;

    public OrderedWriteGate WriterGate { get; } = new OrderedWriteGate();

    public CaptureForwardingCoordinator CaptureForwarding { get; } = new CaptureForwardingCoordinator(maxBufferedCaptureEvents);

    public bool Disconnected { get; private set; }

    public void WriteInputEvent(UInputNative.input_event inputEvent)
    {
        DaemonInputEventEncoder.Write(Writer, inputEvent);
    }

    public void MarkDisconnected()
    {
        Disconnected = true;
    }
}
