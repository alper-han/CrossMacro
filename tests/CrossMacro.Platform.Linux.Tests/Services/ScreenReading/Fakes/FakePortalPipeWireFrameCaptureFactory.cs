
namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading.Fakes;

internal sealed class FakePortalPipeWireFrameCaptureFactory : IPortalPipeWireFrameCaptureFactory
{
    private readonly FakePortalPipeWireFrameCapture? _capture;
    private readonly Queue<FakePortalPipeWireFrameCapture>? _captureSequence;
    private readonly IReadOnlyDictionary<uint, FakePortalPipeWireFrameCapture> _capturesByNodeId;

    public FakePortalPipeWireFrameCaptureFactory(FakePortalPipeWireFrameCapture capture)
    {
        _capture = capture;
        _capturesByNodeId = new Dictionary<uint, FakePortalPipeWireFrameCapture>();
    }

    public FakePortalPipeWireFrameCaptureFactory(IReadOnlyList<FakePortalPipeWireFrameCapture> captures)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(captures.Count, 1);
        _captureSequence = new Queue<FakePortalPipeWireFrameCapture>(captures);
        _capturesByNodeId = new Dictionary<uint, FakePortalPipeWireFrameCapture>();
    }

    public FakePortalPipeWireFrameCaptureFactory(IReadOnlyDictionary<uint, FakePortalPipeWireFrameCapture> capturesByNodeId)
    {
        _capturesByNodeId = capturesByNodeId;
    }

    public int CreateCalls { get; private set; }

    public List<uint> NodeIds { get; } = [];

    public List<SafeFileHandle> RemoteHandles { get; } = [];

    public uint LastNodeId { get; private set; }

    public int LastWidth { get; private set; }

    public int LastHeight { get; private set; }

    public ulong? LastPipeWireSerial { get; private set; }

    public IPortalPipeWireFrameCapture Create(SafeFileHandle pipeWireRemote, uint nodeId, int width, int height)
    {
        CreateCalls++;
        RemoteHandles.Add(pipeWireRemote);
        LastNodeId = nodeId;
        LastWidth = width;
        LastHeight = height;
        NodeIds.Add(nodeId);
        return _capturesByNodeId.TryGetValue(nodeId, out var capture)
            ? capture
            : _captureSequence?.Dequeue()
            ?? _capture
            ?? throw new InvalidOperationException($"No fake PipeWire capture configured for node {nodeId}.");
    }

    public IPortalPipeWireFrameCapture Create(SafeFileHandle pipeWireRemote, PortalStreamDescriptor stream, int width, int height)
    {
        LastPipeWireSerial = stream.PipeWireSerial;
        return Create(pipeWireRemote, stream.NodeId, width, height);
    }
}
