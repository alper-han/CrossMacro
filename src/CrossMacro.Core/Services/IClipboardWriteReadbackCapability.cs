namespace CrossMacro.Core.Services;

/// <summary>
/// Describes a clipboard implementation whose successful write is immediately
/// available to a subsequent read from the same system clipboard.
/// </summary>
public interface IClipboardWriteReadbackCapability
{
    public bool GuaranteesImmediateReadback { get; }
}
