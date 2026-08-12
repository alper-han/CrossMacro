namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;

public sealed class PortalPipeWireFrameValidityTests
{
    [Fact]
    public void IsUsable_WhenChunkIsValid_ReturnsTrue()
    {
        var buffer = new SpaBuffer();
        var chunk = new SpaChunk { Size = 1 };

        Assert.True(PipeWireFrameValidity.IsUsable(buffer, chunk, out var reason));
        Assert.Equal(PipeWireFrameDropReason.None, reason);
    }

    [Fact]
    public void IsUsable_WhenChunkIsCorrupted_DropsTheFrame()
    {
        var buffer = new SpaBuffer();
        var chunk = new SpaChunk { Size = 1, Flags = PipeWireConstants.SpaChunkFlagCorrupted };

        Assert.False(PipeWireFrameValidity.IsUsable(buffer, chunk, out var reason));
        Assert.Equal(PipeWireFrameDropReason.CorruptedChunk, reason);
    }

    [Fact]
    public void IsUsable_WhenChunkIsEmpty_DropsTheFrame()
    {
        var buffer = new SpaBuffer();
        var chunk = new SpaChunk { Size = 1, Flags = PipeWireConstants.SpaChunkFlagEmpty };

        Assert.False(PipeWireFrameValidity.IsUsable(buffer, chunk, out var reason));
        Assert.Equal(PipeWireFrameDropReason.EmptyChunk, reason);
    }

    [Fact]
    public void IsUsable_WhenHeaderIsCorrupted_DropsTheFrame()
    {
        var header = Marshal.AllocHGlobal(32);
        var metadata = Marshal.AllocHGlobal(Marshal.SizeOf<SpaMeta>());
        try
        {
            Marshal.WriteInt32(header, unchecked((int)PipeWireConstants.SpaMetaHeaderFlagCorrupted));
            Marshal.StructureToPtr(new SpaMeta
            {
                Type = PipeWireConstants.SpaMetaHeader,
                Size = 32,
                Data = header,
            }, metadata, fDeleteOld: false);

            var buffer = new SpaBuffer { MetaCount = 1, Metas = metadata };
            var chunk = new SpaChunk { Size = 1 };

            Assert.False(PipeWireFrameValidity.IsUsable(buffer, chunk, out var reason));
            Assert.Equal(PipeWireFrameDropReason.CorruptedHeader, reason);
        }
        finally
        {
            Marshal.FreeHGlobal(metadata);
            Marshal.FreeHGlobal(header);
        }
    }

    [Fact]
    public void IsUsable_WhenHeaderIsMissing_AllowsValidChunk()
    {
        var buffer = new SpaBuffer { MetaCount = 0, Metas = IntPtr.Zero };
        var chunk = new SpaChunk { Size = 1 };

        Assert.True(PipeWireFrameValidity.IsUsable(buffer, chunk, out var reason));
        Assert.Equal(PipeWireFrameDropReason.None, reason);
    }

    [Fact]
    public void IsUsable_WhenChunkPayloadIsEmpty_DropsTheFrame()
    {
        var buffer = new SpaBuffer();
        var chunk = new SpaChunk();

        Assert.False(PipeWireFrameValidity.IsUsable(buffer, chunk, out var reason));
        Assert.Equal(PipeWireFrameDropReason.EmptyPayload, reason);
    }

    [Fact]
    public void IsUsable_WhenUnknownFlagsArePresent_AllowsValidChunk()
    {
        var buffer = new SpaBuffer();
        var chunk = new SpaChunk { Size = 1, Flags = 1u << 31 };

        Assert.True(PipeWireFrameValidity.IsUsable(buffer, chunk, out var reason));
        Assert.Equal(PipeWireFrameDropReason.None, reason);
    }

    [Fact]
    public void IsUsable_WhenTransientCorruptionIsFollowedByValidChunk_AllowsTheNextFrame()
    {
        var buffer = new SpaBuffer();
        var corrupted = new SpaChunk { Size = 1, Flags = PipeWireConstants.SpaChunkFlagCorrupted };
        var valid = new SpaChunk { Size = 1 };

        Assert.False(PipeWireFrameValidity.IsUsable(buffer, corrupted, out _));
        Assert.True(PipeWireFrameValidity.IsUsable(buffer, valid, out var reason));
        Assert.Equal(PipeWireFrameDropReason.None, reason);
    }
}
