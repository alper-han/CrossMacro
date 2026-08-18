using System.Globalization;

namespace CrossMacro.Core.Services.Playback;

/// <summary>
/// Indicates that an absolute cursor move was injected but the compositor did
/// not report the requested position before the next playback event.
/// </summary>
public sealed class AbsoluteCursorMoveNotSettledException : InvalidOperationException
{
    public AbsoluteCursorMoveNotSettledException(int expectedX, int expectedY)
        : base(string.Create(
            CultureInfo.InvariantCulture,
            $"Absolute cursor move did not settle at ({expectedX},{expectedY}); playback cannot safely continue."))
    {
        ExpectedX = expectedX;
        ExpectedY = expectedY;
    }

    public AbsoluteCursorMoveNotSettledException()
        : this(0, 0)
    {
    }

    public AbsoluteCursorMoveNotSettledException(string? message)
        : base(message)
    {
    }

    public AbsoluteCursorMoveNotSettledException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }

    public int ExpectedX { get; }

    public int ExpectedY { get; }
}
