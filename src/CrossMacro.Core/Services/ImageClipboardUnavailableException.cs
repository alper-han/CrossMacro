using System;

namespace CrossMacro.Core.Services;

public sealed class ImageClipboardUnavailableException : InvalidOperationException
{
    public ImageClipboardUnavailableException(string message)
        : base(message)
    {
    }
}
