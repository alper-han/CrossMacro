using CrossMacro.Platform.Linux.DisplayServer.Wayland;
using Microsoft.Win32.SafeHandles;

namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading.Fakes;

internal sealed class FakePortalPipeWireFrameCaptureFactory : IPortalPipeWireFrameCaptureFactory
{
    private readonly FakePortalPipeWireFrameCapture? _capture;
    private readonly IReadOnlyDictionary<uint, FakePortalPipeWireFrameCapture> _capturesByNodeId;

    public FakePortalPipeWireFrameCaptureFactory(FakePortalPipeWireFrameCapture capture)
    {
        _capture = capture;
        _capturesByNodeId = new Dictionary<uint, FakePortalPipeWireFrameCapture>();
    }

    public FakePortalPipeWireFrameCaptureFactory(IReadOnlyDictionary<uint, FakePortalPipeWireFrameCapture> capturesByNodeId)
    {
        _capturesByNodeId = capturesByNodeId;
    }

    public int CreateCalls { get; private set; }

    public List<uint> NodeIds { get; } = [];

    public uint LastNodeId { get; private set; }

    public int LastWidth { get; private set; }

    public int LastHeight { get; private set; }

    public IPortalPipeWireFrameCapture Create(SafeFileHandle pipeWireRemote, uint nodeId, int width, int height)
    {
        CreateCalls++;
        LastNodeId = nodeId;
        LastWidth = width;
        LastHeight = height;
        NodeIds.Add(nodeId);
        return _capturesByNodeId.TryGetValue(nodeId, out var capture)
            ? capture
            : _capture ?? throw new InvalidOperationException($"No fake PipeWire capture configured for node {nodeId}.");
    }
}
